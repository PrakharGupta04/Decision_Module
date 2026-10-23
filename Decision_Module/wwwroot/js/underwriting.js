// Underwriting page interactions: searchable global multi-selects,
// removable chips, decision-specific reason filtering,
// and live Global/Override cover inheritance.
(function () {
    'use strict';

    const globalBlocks = {};

    document.querySelectorAll('.global-block').forEach(function (block) {
        globalBlocks[block.dataset.kind] = block;
    });

    function find(block, selector) {
        return block ? block.querySelector(selector) : null;
    }

    function all(block, selector) {
        return block ? Array.from(block.querySelectorAll(selector)) : [];
    }

    function selectedValues(block, selector) {
        return all(block, selector)
            .filter(function (input) {
                return input.checked;
            })
            .map(function (input) {
                return input.value;
            });
    }

    function selectedGlobalReasons(block) {
        return selectedValues(block, '.js-reason-check');
    }

    function selectedGlobalSubs(block) {
        return selectedValues(block, '.js-sub');
    }

    function decisionFields(block) {
        const select = find(block, '.js-decision');

        if (!select || select.selectedIndex < 0) {
            return [];
        }

        const option = select.options[select.selectedIndex];

        return (
            option && option.dataset.fields
                ? option.dataset.fields
                : ''
        ).split(',');
    }

    function toggleGroup(group, visible) {
        if (!group) return;

        group.hidden = !visible;

        all(group, 'input, select, textarea').forEach(function (input) {
            input.disabled = !visible;
        });
    }

    function clearSubReasonsFor(block, reasonId) {
        all(block, '.js-global-sub-option').forEach(function (option) {
            if (option.dataset.reason === reasonId) {
                const input = find(option, '.js-sub');

                if (input) {
                    input.checked = false;
                }
            }
        });

        all(block, '.js-reason-group').forEach(function (group) {
            if (group.dataset.reason === reasonId) {
                all(group, '.js-sub').forEach(function (input) {
                    input.checked = false;
                });
            }
        });
    }

    function createChip(text, remove) {
        const chip = document.createElement('span');
        chip.className = 'selected-chip';

        const caption = document.createElement('span');
        caption.textContent = text;
        chip.appendChild(caption);

        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = '×';
        button.setAttribute('aria-label', 'Remove ' + text);
        button.addEventListener('click', remove);

        chip.appendChild(button);

        return chip;
    }

    function renderChips(block) {
        const reasonChips = find(block, '.js-reason-chips');
        const subChips = find(block, '.js-sub-chips');

        if (!reasonChips || !subChips) return;

        reasonChips.replaceChildren();
        subChips.replaceChildren();

        all(block, '.js-global-reason-option').forEach(function (option) {
            const input = find(option, '.js-reason-check');

            if (!input || !input.checked) return;

            const label = find(option, 'span');
            const text = label ? label.textContent.trim() : input.value;

            reasonChips.appendChild(
                createChip(text, function () {
                    input.checked = false;
                    clearSubReasonsFor(block, input.value);
                    updateGlobal(block);
                    syncCovers(block.dataset.kind);
                })
            );
        });

        all(block, '.js-global-sub-option').forEach(function (option) {
            const input = find(option, '.js-sub');

            if (!input || !input.checked) return;

            const labels = all(option, 'span');
            const text = labels.length
                ? labels[labels.length - 1].textContent.trim()
                : input.value;

            subChips.appendChild(
                createChip(text, function () {
                    input.checked = false;
                    updateGlobal(block);
                    syncCovers(block.dataset.kind);
                })
            );
        });

        const reasonLabel = find(
            block,
            '.js-global-reason-picker .js-picker-label'
        );

        const subLabel = find(
            block,
            '.js-global-sub-picker .js-picker-label'
        );

        const reasonCount = selectedGlobalReasons(block).length;
        const subCount = selectedGlobalSubs(block).length;

        if (reasonLabel) {
            reasonLabel.textContent = reasonCount
                ? reasonCount + ' selected'
                : 'Select reasons...';
        }

        if (subLabel) {
            subLabel.textContent = subCount
                ? subCount + ' selected'
                : 'Select sub-reasons...';
        }
    }

    function filterGlobalOptions(block, type, query) {
        const normalized = (query || '').trim().toLowerCase();
        const isReason = type === 'reason';

        const selector = isReason
            ? '.js-global-reason-option'
            : '.js-global-sub-option';

        const decision = find(block, '.js-decision');
        const activeReasons = selectedGlobalReasons(block);

        all(block, selector).forEach(function (option) {
            const text = (
                option.dataset.search || option.textContent
            ).toLowerCase();

            const matchesSearch = text.includes(normalized);

            let matchesContext;

            if (isReason) {
                matchesContext =
                    !decision ||
                    !decision.value ||
                    option.dataset.decision === decision.value;
            } else {
                matchesContext = activeReasons.includes(
                    option.dataset.reason
                );
            }

            option.hidden = !(matchesSearch && matchesContext);
        });
    }

    function refreshGlobalOptions(block) {
        const decision = find(block, '.js-decision');

        if (!decision) return;

        all(block, '.js-global-reason-option').forEach(function (option) {
            const input = find(option, '.js-reason-check');

            const fits =
                !decision.value ||
                option.dataset.decision === decision.value;

            option.hidden = !fits;

            if (!fits && input && input.checked) {
                input.checked = false;
                clearSubReasonsFor(block, input.value);
            }
        });

        const activeReasons = selectedGlobalReasons(block);

        all(block, '.js-global-sub-option').forEach(function (option) {
            const input = find(option, '.js-sub');

            const fits = activeReasons.includes(option.dataset.reason);

            option.hidden = !fits;

            if (!fits && input) {
                input.checked = false;
            }
        });

        const reasonSearch = find(block, '.js-reason-search');
        const subSearch = find(block, '.js-sub-search');

        filterGlobalOptions(
            block,
            'reason',
            reasonSearch ? reasonSearch.value : ''
        );

        filterGlobalOptions(
            block,
            'sub',
            subSearch ? subSearch.value : ''
        );
    }

    function refreshCover(block, locked) {
        const decision = find(block, '.js-decision');

        if (!decision) return;

        const fields = decisionFields(block);
        let reasonCount = 0;
        let subCount = 0;

        all(block, '.js-reason-group').forEach(function (group) {
            const reasonInput = find(group, '.js-reason-check');

            const fits =
                !decision.value ||
                group.dataset.decision === decision.value;

            group.hidden = !fits;

            if (reasonInput) {
                reasonInput.disabled = locked || !fits;

                if (!fits) {
                    reasonInput.checked = false;

                    all(group, '.js-sub').forEach(function (sub) {
                        sub.checked = false;
                    });
                }

                if (fits) reasonCount++;
            }

            all(group, '.js-sub-item').forEach(function (item) {
                const sub = find(item, '.js-sub');

                item.hidden = !fits;

                if (sub) {
                    sub.disabled = locked || !fits;

                    if (!fits) {
                        sub.checked = false;
                    }

                    if (fits) subCount++;
                }
            });
        });

        decision.disabled = locked;

        const requiredReason = find(block, '.js-reason-required');

        if (requiredReason) {
            requiredReason.hidden = reasonCount === 0;
        }

        const requiredSub = find(block, '.js-sub-required');

        if (requiredSub) {
            requiredSub.hidden = subCount === 0;
        }

        toggleGroup(
            find(block, '.js-postpone'),
            fields.includes('PostponePeriod')
        );

        toggleGroup(
            find(block, '.js-extras'),
            fields.includes('Extras')
        );
    }

    function updateGlobal(block) {
        const enabled = !!find(block, '.js-on')?.checked;

        refreshGlobalOptions(block);
        renderChips(block);

        const decision = find(block, '.js-decision');

        if (decision) {
            decision.disabled = !enabled;
        }

        all(block, '.js-reason-check, .js-sub').forEach(function (input) {
            input.disabled = !enabled;
        });

        all(
            block,
            '.multi-picker summary, .multi-picker-menu input[type="search"]'
        ).forEach(function (element) {
            element.setAttribute(
                'aria-disabled',
                enabled ? 'false' : 'true'
            );

            if ('disabled' in element) {
                element.disabled = !enabled;
            }
        });

        const fields = decisionFields(block);

        toggleGroup(
            find(block, '.js-postpone'),
            fields.includes('PostponePeriod')
        );

        toggleGroup(
            find(block, '.js-extras'),
            fields.includes('Extras')
        );
    }

    function copyGlobalToCover(cover, globalBlock) {
        if (!globalBlock) return;

        const globalDecision = find(globalBlock, '.js-decision');
        const coverDecision = find(cover, '.js-decision');

        if (globalDecision && coverDecision) {
            coverDecision.value = globalDecision.value;
        }

        all(cover, '.js-reason-check, .js-sub').forEach(function (input) {
            input.checked = false;
        });

        selectedGlobalReasons(globalBlock).forEach(function (reasonId) {
            const target = all(cover, '.js-reason-check').find(
                function (input) {
                    return input.value === reasonId;
                }
            );

            if (target) {
                target.checked = true;
            }
        });

        selectedGlobalSubs(globalBlock).forEach(function (subId) {
            const target = all(cover, '.js-sub').find(function (input) {
                return input.value === subId;
            });

            if (target) {
                target.checked = true;
            }
        });

        const globalNote = find(globalBlock, '.js-global-notes');
        const inheritedNote = find(cover, '.js-inherited-note');

        if (globalNote && inheritedNote) {
            inheritedNote.value = globalNote.value;
        }
    }

    function syncCover(cover) {
        const source = find(cover, '.js-source');

        if (!source) return;

        const globalBlock = globalBlocks[cover.dataset.kind];

        const globalEnabled =
            globalBlock && find(globalBlock, '.js-on')?.checked;

        const useGlobal =
            source.value === 'GLOBAL' && globalEnabled;

        if (useGlobal) {
            copyGlobalToCover(cover, globalBlock);
            refreshCover(cover, true);

            const globalNoteArea = find(cover, '.js-global-note-area');
            const overrideNotes = find(cover, '.js-override-notes');

            if (globalNoteArea) {
                globalNoteArea.hidden = false;
            }

            if (overrideNotes) {
                overrideNotes.hidden = true;

                all(overrideNotes, 'input, textarea').forEach(
                    function (input) {
                        input.disabled = true;
                    }
                );
            }
        } else {
            refreshCover(cover, false);

            const globalNoteArea = find(cover, '.js-global-note-area');
            const overrideNotes = find(cover, '.js-override-notes');

            if (globalNoteArea) {
                globalNoteArea.hidden = true;
            }

            if (overrideNotes) {
                overrideNotes.hidden = false;

                all(overrideNotes, 'input, textarea').forEach(
                    function (input) {
                        input.disabled = false;
                    }
                );
            }
        }
    }

    function syncCovers(kind) {
        document.querySelectorAll('.cover-block').forEach(function (cover) {
            if (cover.dataset.kind === kind) {
                syncCover(cover);
            }
        });
    }

    function applyGlobalSwitch(block) {
        const enabled = !!find(block, '.js-on')?.checked;

        updateGlobal(block);

        document.querySelectorAll('.cover-block').forEach(function (cover) {
            if (cover.dataset.kind !== block.dataset.kind) return;

            const source = find(cover, '.js-source');

            const globalOption = source
                ? source.querySelector('option[value="GLOBAL"]')
                : null;

            if (globalOption) {
                globalOption.disabled = !enabled;
            }

            if (!enabled && source && source.value === 'GLOBAL') {
                source.value = 'OVERRIDE';
            }

            syncCover(cover);
        });
    }

    document.addEventListener('input', function (event) {
        const target = event.target;
        const block = target.closest('.global-block');

        if (!block) return;

        if (target.matches('.js-reason-search')) {
            filterGlobalOptions(block, 'reason', target.value);
        }

        if (target.matches('.js-sub-search')) {
            filterGlobalOptions(block, 'sub', target.value);
        }

        if (target.matches('.js-global-notes')) {
            syncCovers(block.dataset.kind);
        }
    });

    document.addEventListener('input', function (event) {
        const target = event.target;

        if (!target.matches('.js-reason-search')) return;

        const block = target.closest('.cover-block');

        if (!block) return;

        const query = target.value.trim().toLowerCase();

        all(block, '.js-reason-group').forEach(function (group) {
            const reasonText = (
                group.dataset.reasonText || group.textContent
            ).toLowerCase();

            const reasonMatches = reasonText.includes(query);

            let subMatches = false;

            all(group, '.js-sub-item').forEach(function (item) {
                const subText = (
                    item.dataset.subreasonText || item.textContent
                ).toLowerCase();

                const matches = subText.includes(query);

                if (matches) subMatches = true;

                item.hidden = query !== '' && !matches;
            });

            group.hidden =
                query !== '' && !reasonMatches && !subMatches;
        });
    });

    document.addEventListener('change', function (event) {
        const target = event.target;
        const block = target.closest('.block');

        if (!block) return;

        if (target.matches('.js-on')) {
            applyGlobalSwitch(block);
            return;
        }

        if (target.matches('.js-source')) {
            syncCover(block);
            return;
        }

        if (target.matches('.js-decision')) {
            all(block, '.js-reason-check, .js-sub').forEach(
                function (input) {
                    input.checked = false;
                }
            );

            if (block.classList.contains('global-block')) {
                updateGlobal(block);
                syncCovers(block.dataset.kind);
            } else {
                refreshCover(block, false);
            }

            return;
        }

        if (target.matches('.js-reason-check')) {
            if (block.classList.contains('global-block')) {
                if (!target.checked) {
                    clearSubReasonsFor(block, target.value);
                }

                updateGlobal(block);
                syncCovers(block.dataset.kind);
            } else {
                if (!target.checked) {
                    const group = target.closest('.js-reason-group');

                    if (group) {
                        all(group, '.js-sub').forEach(function (input) {
                            input.checked = false;
                        });
                    }
                }

                refreshCover(block, false);
            }

            return;
        }

        if (target.matches('.js-sub')) {
            if (block.classList.contains('global-block')) {
                updateGlobal(block);
                syncCovers(block.dataset.kind);
            }
        }
    });

    document.addEventListener('click', function (event) {
        document.querySelectorAll('.multi-picker[open]').forEach(
            function (picker) {
                if (!picker.contains(event.target)) {
                    picker.removeAttribute('open');
                }
            }
        );
    });

    Object.values(globalBlocks).forEach(function (block) {
        updateGlobal(block);
        applyGlobalSwitch(block);
    });

    document.querySelectorAll('.cover-block').forEach(syncCover);
})();