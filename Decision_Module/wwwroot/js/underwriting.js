// For every decision form (global cards and cover editors):
//  - show only the reasons that belong to the chosen decision
//  - show the postpone period / extras only if the chosen decision uses them
// Hidden inputs are disabled so they are not sent. The server checks everything again anyway.
document.querySelectorAll('[data-decision-form]').forEach(function (box) {
    var decision = box.querySelector('.js-decision');
    var reason   = box.querySelector('.js-reason');
    var hint     = box.querySelector('.js-reason-hint');
    var postpone = box.querySelector('.js-postpone');
    var extras   = box.querySelector('.js-extras');
    if (!decision || !reason) return;

    function show(el, visible) {
        if (!el) return;
        el.hidden = !visible;
        el.querySelectorAll('input').forEach(function (i) { i.disabled = !visible; });
    }

    function refresh() {
        var chosen = decision.options[decision.selectedIndex];
        var fields = (chosen && chosen.dataset.fields ? chosen.dataset.fields : '').split(',');

        var matches = 0;
        reason.querySelectorAll('option[data-decision]').forEach(function (o) {
            var match = o.dataset.decision === decision.value;
            o.hidden = !match;
            o.disabled = !match;
            if (match) { matches++; }
            else if (o.selected) { reason.value = ''; }
        });
        reason.disabled = matches === 0;
        hint.textContent = (decision.value && matches === 0) ? 'This decision has no reasons.' : '';

        show(postpone, fields.indexOf('PostponePeriod') >= 0);
        show(extras, fields.indexOf('Extras') >= 0);
    }

    decision.addEventListener('change', function () { reason.value = ''; refresh(); });
    refresh();
});