// For every decision form: show the postpone period and extras only for decisions that use them,
// and mark the reason as required for decisions that need one. The server checks all of this again.
document.querySelectorAll('[data-decision-form]').forEach(function (form) {
    var decision = form.querySelector('.js-decision');
    var reason = form.querySelector('.js-reason');
    var star = form.querySelector('.js-reason-star');
    var postpone = form.querySelector('.js-postpone');
    var extras = form.querySelector('.js-extras');
    if (!decision) return;

    function setVisible(box, visible) {
        if (!box) return;
        box.hidden = !visible;
        box.querySelectorAll('input').forEach(function (input) { input.disabled = !visible; });
    }

    function refresh() {
        var option = decision.options[decision.selectedIndex];
        var fields = option && option.dataset.fields ? option.dataset.fields.split(',') : [];
        var reasonRequired = fields.indexOf('ReasonRequired') >= 0;

        if (reason) reason.required = reasonRequired;
        if (star) star.hidden = !reasonRequired;
        setVisible(postpone, fields.indexOf('PostponePeriod') >= 0);
        setVisible(extras, fields.indexOf('Extras') >= 0);
    }

    decision.addEventListener('change', refresh);
    refresh();
});