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
