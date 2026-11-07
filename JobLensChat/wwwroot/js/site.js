// Chat composer: character count, sample questions, and a single in-flight submit.
(function () {
    var form = document.getElementById("ask-form");
    var question = document.getElementById("Question");
    var counter = document.getElementById("char-count");
    var button = document.getElementById("ask-button");
    if (!form || !question || !counter || !button) {
        return;
    }

    function updateCount() {
        counter.textContent = question.value.length + " / 500";
    }

    question.addEventListener("input", updateCount);
    updateCount();

    document.querySelectorAll(".sample-question").forEach(function (sample) {
        sample.addEventListener("click", function () {
            question.value = (sample.textContent || "").trim();
            updateCount();
            question.focus();
        });
    });

    form.addEventListener("submit", function () {
        button.disabled = true;
        button.textContent = "Thinking…";
        button.setAttribute("aria-busy", "true");
    });
})();

(function () {
    var toggle = document.querySelector("[data-menu-toggle]");
    var nav = document.getElementById("site-nav");
    if (toggle && nav) {
        toggle.addEventListener("click", function () {
            var open = nav.classList.toggle("is-open");
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
    }

    function closeMenus() {
        document.querySelectorAll("[data-dropdown-button]").forEach(function (button) {
            button.setAttribute("aria-expanded", "false");
        });
        document.querySelectorAll("[data-dropdown-panel]").forEach(function (panel) {
            panel.hidden = true;
        });
    }

    document.querySelectorAll("[data-dropdown]").forEach(function (menu) {
        var button = menu.querySelector("[data-dropdown-button]");
        var panel = menu.querySelector("[data-dropdown-panel]");
        if (!button || !panel) {
            return;
        }

        button.addEventListener("click", function () {
            var open = button.getAttribute("aria-expanded") === "true";
            closeMenus();
            if (!open) {
                button.setAttribute("aria-expanded", "true");
                panel.hidden = false;
                var first = panel.querySelector("a");
                if (first) {
                    first.focus();
                }
            }
        });
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeMenus();
        }
    });

    document.addEventListener("click", function (event) {
        if (!event.target.closest("[data-dropdown]")) {
            closeMenus();
        }
    });

    document.querySelectorAll(".alert-temp").forEach(function (alert) {
        window.setTimeout(function () {
            alert.hidden = true;
        }, 5000);
    });

    var source = document.querySelector("[data-domain-source]");
    var preview = document.getElementById("domain-preview");
    if (source && preview) {
        var showDomain = function () {
            var email = source.value || "";
            var at = email.lastIndexOf("@");
            var domain = at >= 0 && at < email.length - 1 ? email.slice(at + 1).trim().toLowerCase() : "";
            preview.textContent = domain.length
                ? "Domain preview: " + domain
                : "Domain is taken from the email and is not typed separately.";
        };
        source.addEventListener("input", showDomain);
        showDomain();
    }

    document.querySelectorAll("[data-counter]").forEach(function (field) {
        var target = document.getElementById(field.getAttribute("data-counter"));
        if (!target) {
            return;
        }

        var limit = field.getAttribute("maxlength") || "500";
        var update = function () {
            target.textContent = field.value.length + " / " + limit + " characters.";
        };
        field.addEventListener("input", update);
        update();
    });

    document.querySelectorAll("[data-tab]").forEach(function (button) {
        button.addEventListener("click", function () {
            var target = document.getElementById(button.getAttribute("data-tab"));
            document.querySelectorAll("[data-tab]").forEach(function (other) {
                var selected = other === button;
                other.setAttribute("aria-selected", selected ? "true" : "false");
            });
            document.querySelectorAll("[role='tabpanel']").forEach(function (panel) {
                panel.hidden = target ? panel !== target : false;
            });
        });
    });
})();
