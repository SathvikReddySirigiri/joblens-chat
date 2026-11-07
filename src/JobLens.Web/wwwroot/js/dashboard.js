(function () {
    var rangeSelect = document.getElementById("dashboard-range");
    var refreshButton = document.getElementById("dashboard-refresh");
    if (!rangeSelect || !refreshButton || typeof Chart === "undefined") {
        return;
    }

    var styles = getComputedStyle(document.documentElement);
    function color(name) {
        return styles.getPropertyValue(name).trim();
    }

    var palette = ["--chart-1", "--chart-2", "--chart-3", "--chart-4", "--chart-5", "--chart-6"].map(color);
    var grid = color("--border") || "#E2E8F0";
    var text = color("--text") || "#1E293B";
    var charts = {};

    var centerTotal = {
        id: "centerTotal",
        afterDraw: function (chart) {
            if (chart.config.type !== "doughnut") {
                return;
            }
            var values = chart.data.datasets[0].data;
            var total = values.reduce(function (sum, value) { return sum + Number(value || 0); }, 0);
            var area = chart.chartArea;
            var ctx = chart.ctx;
            ctx.save();
            ctx.font = "600 22px Inter, sans-serif";
            ctx.fillStyle = text;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText(String(total), (area.left + area.right) / 2, (area.top + area.bottom) / 2);
            ctx.restore();
        }
    };

    function gridScale(stacked) {
        return {
            beginAtZero: true,
            stacked: stacked,
            ticks: { precision: 0 },
            grid: { color: grid }
        };
    }

    function percentOf(value, total) {
        if (!total) {
            return "0%";
        }
        return (100 * value / total).toLocaleString("en-US", { maximumFractionDigits: 1 }) + "%";
    }

    function pointValue(item) {
        if (item.chart.options.indexAxis === "y") {
            return Number(item.parsed.x);
        }
        return Number(item.parsed && item.parsed.y !== undefined ? item.parsed.y : item.parsed);
    }

    function shareTotal(item) {
        var data = item.chart.data;
        if (item.chart.$share === "first") {
            return Number(data.datasets[0].data[item.dataIndex] || 0);
        }
        if (item.chart.$share === "dataset") {
            return data.datasets[item.datasetIndex].data.reduce(function (sum, value) { return sum + Number(value || 0); }, 0);
        }
        return data.datasets.reduce(function (sum, set) { return sum + Number(set.data[item.dataIndex] || 0); }, 0);
    }

    var shareTooltip = {
        callbacks: {
            label: function (item) {
                var value = pointValue(item);
                return item.dataset.label + ": " + value + " (" + percentOf(value, shareTotal(item)) + ")";
            }
        }
    };

    function baseOptions(extra) {
        var options = {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: "bottom" },
                tooltip: extra.tooltip
            }
        };
        if (extra.indexAxis) {
            options.indexAxis = extra.indexAxis;
        }
        if (extra.scales) {
            options.scales = extra.scales;
        }
        return options;
    }

    function upsert(key, config) {
        if (charts[key]) {
            charts[key].data.labels = config.data.labels;
            config.data.datasets.forEach(function (source, index) {
                charts[key].data.datasets[index].data = source.data;
                if (source.backgroundColor) {
                    charts[key].data.datasets[index].backgroundColor = source.backgroundColor;
                }
            });
            charts[key].$meta = config.$meta || null;
            charts[key].update();
            return;
        }
        var chart = new Chart(document.getElementById(key), config);
        chart.$share = config.$share || "stack";
        chart.$meta = config.$meta || null;
        charts[key] = chart;
    }

    function setRows(id, rows) {
        var body = document.getElementById(id);
        body.replaceChildren();
        if (rows.length === 0) {
            var empty = document.createElement("tr");
            var cell = document.createElement("td");
            cell.colSpan = 4;
            cell.textContent = "No data in this range.";
            empty.appendChild(cell);
            body.appendChild(empty);
            return;
        }
        rows.forEach(function (cells) {
            var row = document.createElement("tr");
            cells.forEach(function (value) {
                var cell = document.createElement("td");
                cell.textContent = value;
                row.appendChild(cell);
            });
            body.appendChild(row);
        });
    }

    function money(value, wholeWhenEven) {
        var even = wholeWhenEven && Math.abs(Number(value) - Math.round(Number(value))) < 0.001;
        return "$" + Number(value).toLocaleString("en-US", {
            minimumFractionDigits: even ? 0 : 2,
            maximumFractionDigits: even ? 0 : 2
        });
    }

    function apply(data) {
        var kpis = data.kpis;
        document.getElementById("kpi-total").textContent = kpis.totalReports;
        document.getElementById("kpi-pending").textContent = kpis.pendingReports;
        document.getElementById("kpi-approval").textContent = Number(kpis.approvalRate).toLocaleString("en-US", { maximumFractionDigits: 1 }) + "%";
        document.getElementById("kpi-money").textContent = money(kpis.totalAmountRequestedUsd, true);
        document.getElementById("kpi-checks").textContent = kpis.checksThisMonth;
        document.getElementById("kpi-high").textContent = Number(kpis.highRiskCheckPct).toLocaleString("en-US", { maximumFractionDigits: 1 }) + "%";

        var insightList = document.getElementById("dashboard-insights");
        insightList.replaceChildren();
        data.insights.forEach(function (insight) {
            var item = document.createElement("li");
            item.textContent = typeof insight === "string" ? insight : insight.text;
            insightList.appendChild(item);
        });

        var stamp = document.getElementById("dashboard-updated");
        var now = new Date();
        stamp.dateTime = now.toISOString();
        stamp.textContent = now.toISOString().slice(0, 16).replace("T", " ") + " UTC";

        var months = data.reportsPerMonth;
        upsert("chart-months", {
            type: "line",
            data: {
                labels: months.map(function (row) { return row.month; }),
                datasets: [
                    { label: "All reports", data: months.map(function (row) { return row.total; }), borderColor: palette[0], backgroundColor: palette[0], tension: 0.2 },
                    { label: "Approved", data: months.map(function (row) { return row.approved; }), borderColor: palette[1], backgroundColor: palette[1], tension: 0.2 }
                ]
            },
            options: baseOptions({
                scales: { x: { grid: { color: grid } }, y: gridScale(false) },
                tooltip: shareTooltip
            }),
            $share: "first"
        });
        setRows("table-months", months.map(function (row) { return [row.month, String(row.total), String(row.approved)]; }));

        var types = data.reportsByScamType;
        var typeTotal = types.reduce(function (sum, row) { return sum + row.count; }, 0);
        upsert("chart-types", {
            type: "doughnut",
            data: {
                labels: types.map(function (row) { return row.category; }),
                datasets: [{ label: "Reports", data: types.map(function (row) { return row.count; }), backgroundColor: palette }]
            },
            options: baseOptions({ tooltip: shareTooltip }),
            plugins: [centerTotal],
            $share: "dataset"
        });
        setRows("table-types", types.map(function (row) { return [row.category, String(row.count), percentOf(row.count, typeTotal)]; }));

        var companies = data.topImpersonatedCompanies;
        upsert("chart-companies", {
            type: "bar",
            data: {
                labels: companies.map(function (row) { return row.companyName; }),
                datasets: [{ label: "Approved reports", data: companies.map(function (row) { return row.approvedReports; }), backgroundColor: palette[0] }]
            },
            options: baseOptions({
                indexAxis: "y",
                scales: { x: gridScale(false), y: { grid: { color: grid } } },
                tooltip: {
                    callbacks: {
                        label: function (item) {
                            var row = item.chart.$meta[item.dataIndex];
                            var total = item.chart.$meta.reduce(function (sum, company) { return sum + company.approvedReports; }, 0);
                            return row.approvedReports + " approved (" + percentOf(row.approvedReports, total) + "), " + money(row.totalAmountRequestedUsd);
                        }
                    }
                }
            }),
            $meta: companies
        });
        setRows("table-companies", companies.map(function (row) {
            return [row.companyName, String(row.approvedReports), money(row.totalAmountRequestedUsd)];
        }));

        var checks = data.checksByVerdictPerMonth;
        upsert("chart-checks", {
            type: "bar",
            data: {
                labels: checks.map(function (row) { return row.month; }),
                datasets: [
                    { label: "LOW", data: checks.map(function (row) { return row.low; }), backgroundColor: palette[1], stack: "verdict" },
                    { label: "MEDIUM", data: checks.map(function (row) { return row.medium; }), backgroundColor: palette[2], stack: "verdict" },
                    { label: "HIGH", data: checks.map(function (row) { return row.high; }), backgroundColor: palette[3], stack: "verdict" }
                ]
            },
            options: baseOptions({
                scales: { x: { stacked: true, grid: { color: grid } }, y: gridScale(true) },
                tooltip: shareTooltip
            }),
            $share: "stack"
        });
        setRows("table-checks", checks.map(function (row) { return [row.month, String(row.low), String(row.medium), String(row.high)]; }));

        var statuses = data.reportsByStatus;
        var statusTotal = statuses.reduce(function (sum, row) { return sum + row.count; }, 0);
        upsert("chart-status", {
            type: "bar",
            data: {
                labels: statuses.map(function (row) { return row.category; }),
                datasets: [{
                    label: "Reports",
                    data: statuses.map(function (row) { return row.count; }),
                    backgroundColor: [palette[2], palette[1], palette[3]]
                }]
            },
            options: baseOptions({
                scales: { x: { grid: { color: grid } }, y: gridScale(false) },
                tooltip: shareTooltip
            }),
            $share: "dataset"
        });
        setRows("table-status", statuses.map(function (row) { return [row.category, String(row.count), percentOf(row.count, statusTotal)]; }));

        var compare = data.companyActivity || data.postingsVsReports || [];
        upsert("chart-compare", {
            type: "bar",
            data: {
                labels: compare.map(function (row) { return row.companyName; }),
                datasets: [
                    { label: "Postings", data: compare.map(function (row) { return row.postings; }), backgroundColor: palette[0] },
                    { label: "Approved reports", data: compare.map(function (row) { return row.approvedReports; }), backgroundColor: palette[4] }
                ]
            },
            options: baseOptions({
                scales: { x: { grid: { color: grid } }, y: gridScale(false) },
                tooltip: {
                    callbacks: {
                        label: function (item) {
                            var rows = item.chart.data.datasets.map(function (set) { return Number(set.data[item.dataIndex] || 0); });
                            var total = rows.reduce(function (sum, value) { return sum + value; }, 0);
                            return item.dataset.label + ": " + pointValue(item) + " (" + percentOf(pointValue(item), total) + ")";
                        },
                        afterBody: function (items) {
                            var row = items[0].chart.$meta[items[0].dataIndex];
                            return row.averageRating == null ? "No interview rating" : "Average rating " + Number(row.averageRating).toFixed(1);
                        }
                    }
                }
            }),
            $meta: compare
        });
        setRows("table-compare", compare.map(function (row) {
            var rating = row.averageRating == null ? "—" : Number(row.averageRating).toFixed(1);
            return [row.companyName, String(row.postings), String(row.approvedReports), rating];
        }));
    }

    async function refresh() {
        var error = document.getElementById("dashboard-error");
        try {
            var response = await fetch("/Dashboard/Data?range=" + encodeURIComponent(rangeSelect.value), {
                headers: { "Accept": "application/json" },
                cache: "no-store"
            });
            if (!response.ok) {
                throw new Error("status");
            }
            apply(await response.json());
            error.hidden = true;
        } catch (ignored) {
            error.hidden = false;
        }
    }

    rangeSelect.addEventListener("change", refresh);
    refreshButton.addEventListener("click", refresh);
    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "visible") {
            refresh();
        }
    });
    refresh();
})();
