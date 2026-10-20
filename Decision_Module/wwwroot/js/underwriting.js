// Underwriting page: dependent dropdowns, inherited global values and the global on/off switch.

var globalBlocks = {};
document.querySelectorAll('.global-block').forEach(function (block) {
    globalBlocks[block.dataset.kind] = block;
});

function find(block, selector) {
    return block.querySelector(selector);
}

// Hidden inputs are disabled so they are not posted.
function setVisible(group, visible) {
    if (!group) return;
    group.hidden = !visible;
    group.querySelectorAll('input').forEach(function (input) {
        input.disabled = !visible;
    });
}

// Brings one block (a global decision or a cover) in line with its current values.
function refresh(block, locked) {
    var decision = find(block, '.js-decision');
    var reason = find(block, '.js-reason');
    var fields = decision.options[decision.selectedIndex].dataset.fields || '';

    var reasonCount = 0;
    reason.querySelectorAll('option[data-decision]').forEach(function (option) {
        var fits = option.dataset.decision === decision.value;
        option.hidden = !fits;
        option.disabled = !fits;
        if (fits) reasonCount++;
    });
    if (reason.selectedOptions[0].disabled) reason.value = '';

    var subCount = 0;
    block.querySelectorAll('.js-sub-item').forEach(function (item) {
        var fits = reason.value !== '' && item.dataset.reason === reason.value;
        var box = item.querySelector('input');
        item.hidden = !fits;
        if (!fits) box.checked = false;
        box.disabled = locked;
        if (fits) subCount++;
    });

    decision.disabled = locked;
    reason.disabled = locked || reasonCount === 0;
    find(block, '.js-reason-required').hidden = reasonCount === 0;
    find(block, '.js-sub-required').hidden = subCount === 0;

    setVisible(find(block, '.js-postpone'), fields.indexOf('PostponePeriod') >= 0);
    setVisible(find(block, '.js-extras'), fields.indexOf('Extras') >= 0);
}

// Copies the global decision of the cover's kind onto the cover.
function copyFromGlobal(cover) {
    var globalBlock = globalBlocks[cover.dataset.kind];

    find(cover, '.js-decision').value = find(globalBlock, '.js-decision').value;
    refresh(cover, false);
    find(cover, '.js-reason').value = find(globalBlock, '.js-reason').value;

    cover.querySelectorAll('.js-sub').forEach(function (box) {
        var match = globalBlock.querySelector('.js-sub[value="' + box.value + '"]');
        box.checked = match ? match.checked : false;
    });
}

// A Global cover shows the global values and is locked. An Override cover is editable.
function syncCover(cover) {
    if (find(cover, '.js-source').value === 'GLOBAL') {
        copyFromGlobal(cover);
        refresh(cover, true);
    } else {
        refresh(cover, false);
    }
}

function syncCovers(kind) {
    document.querySelectorAll('.cover-block[data-kind="' + kind + '"]').forEach(syncCover);
}

// Unticked: every cover of this kind becomes Override and keeps the values it was showing.
function applyGlobalSwitch(kind) {
    var block = globalBlocks[kind];
    var on = find(block, '.js-on').checked;
    refresh(block, !on);

    document.querySelectorAll('.cover-block[data-kind="' + kind + '"]').forEach(function (cover) {
        var source = find(cover, '.js-source');
        source.querySelector('option[value="GLOBAL"]').disabled = !on;
        if (!on) source.value = 'OVERRIDE';
        syncCover(cover);
    });
}

document.addEventListener('change', function (event) {
    var target = event.target;
    var block = target.closest('.block');
    if (!block || !target.matches('.js-on, .js-source, .js-decision, .js-reason, .js-sub')) return;

    if (target.matches('.js-decision')) find(block, '.js-reason').value = '';
    if (target.matches('.js-decision, .js-reason')) {
        block.querySelectorAll('.js-sub').forEach(function (box) { box.checked = false; });
    }

    if (!block.classList.contains('global-block')) {
        syncCover(block);
    } else if (target.matches('.js-on')) {
        applyGlobalSwitch(block.dataset.kind);
    } else {
        refresh(block, false);
        syncCovers(block.dataset.kind);
    }
});

Object.keys(globalBlocks).forEach(applyGlobalSwitch);