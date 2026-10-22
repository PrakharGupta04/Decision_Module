
(function () {
    "use strict";

    const globalBlocks = {};

    document.querySelectorAll(".global-block").forEach(block => {
        globalBlocks[block.dataset.kind] = block;
    });

    function find(block, selector) {
        return block ? block.querySelector(selector) : null;
    }

    function refresh(block, locked) {
        if (!block) return;

        const decision = find(block, ".js-decision");
        const fields = decision?.selectedOptions?.[0]?.dataset.fields || "";

        block.querySelectorAll(".js-reason-group").forEach(group => {
            const reasonCheck = find(group, ".js-reason-check");
            const fits = !!decision &&
                decision.value !== "" &&
                group.dataset.decision === decision.value;

            group.hidden = !fits;

            if (!fits && reasonCheck) {
                reasonCheck.checked = false;
            }

            if (reasonCheck) {
                reasonCheck.disabled = locked || !fits;
            }

            group.querySelectorAll(".js-sub-item").forEach(item => {
                const sub = find(item, ".js-sub");
                const active = fits && !!reasonCheck && reasonCheck.checked;

                item.hidden = !active;

                if (!active && sub) {
                    sub.checked = false;
                }

                if (sub) {
                    sub.disabled = locked || !active;
                }
            });
        });

        if (decision) {
            decision.disabled = locked;
        }

        const postpone = find(block, ".js-postpone");
        if (postpone) {
            postpone.hidden = !fields.includes("PostponePeriod");
            postpone.querySelectorAll("input,select,textarea").forEach(el => {
                el.disabled = postpone.hidden;
            });
        }

        const extras = find(block, ".js-extras");
        if (extras) {
            extras.hidden = !fields.includes("Extras");
            extras.querySelectorAll("input,select,textarea").forEach(el => {
                el.disabled = extras.hidden;
            });
        }
    }

    function copyFromGlobal(cover) {
        const globalBlock = globalBlocks[cover.dataset.kind];
        if (!globalBlock) return;

        const globalDecision = find(globalBlock, ".js-decision");
        const coverDecision = find(cover, ".js-decision");

        if (globalDecision && coverDecision) {
            coverDecision.value = globalDecision.value;
        }

        cover.querySelectorAll(".js-reason-group").forEach(group => {
            const selector =
                '.js-reason-group[data-reason="' + group.dataset.reason + '"]';

            const globalGroup = globalBlock.querySelector(selector);
            const targetReason = find(group, ".js-reason-check");
            const sourceReason = find(globalGroup, ".js-reason-check");

            if (targetReason) {
                targetReason.checked = !!sourceReason && sourceReason.checked;
            }

            group.querySelectorAll(".js-sub").forEach(targetSub => {
                const sourceSub = globalGroup
                    ? globalGroup.querySelector(
                        '.js-sub[value="' + targetSub.value + '"]'
                    )
                    : null;

                targetSub.checked = !!sourceSub && sourceSub.checked;
            });
        });

        const globalNotes = find(globalBlock, ".js-global-notes");
        const inheritedNote = find(cover, ".js-inherited-note");

        if (inheritedNote) {
            inheritedNote.value = globalNotes ? globalNotes.value : "";
        }
    }

    function syncCover(cover) {
        const source = find(cover, ".js-source");
        const locked = !!source && source.value === "GLOBAL";

        if (locked) {
            copyFromGlobal(cover);
        }

        refresh(cover, locked);

        const inheritedArea = find(cover, ".js-global-note-area");
        const overrideArea = find(cover, ".js-override-notes");

        if (inheritedArea) {
            inheritedArea.hidden = !locked;
        }

        if (overrideArea) {
            overrideArea.hidden = locked;

            overrideArea.querySelectorAll("input,textarea").forEach(el => {
                el.disabled = locked;
            });
        }
    }

    function syncCovers(kind) {
        document.querySelectorAll(".cover-block").forEach(cover => {
            if (cover.dataset.kind === kind) {
                syncCover(cover);
            }
        });
    }

    function applyGlobalSwitch(kind) {
        const block = globalBlocks[kind];
        if (!block) return;

        const onBox = find(block, ".js-on");
        const isOn = !!onBox && onBox.checked;

        refresh(block, !isOn);

        document.querySelectorAll(".cover-block").forEach(cover => {
            if (cover.dataset.kind !== kind) return;

            const source = find(cover, ".js-source");
            const globalOption = source?.querySelector(
                'option[value="GLOBAL"]'
            );

            if (globalOption) {
                globalOption.disabled = !isOn;
            }

            if (!isOn && source && source.value === "GLOBAL") {
                source.value = "OVERRIDE";
            }

            syncCover(cover);
        });
    }

    document.addEventListener("change", event => {
        const target = event.target;
        const block = target.closest(".block");

        if (!block) return;

        if (target.matches(".js-on")) {
            applyGlobalSwitch(block.dataset.kind);
            return;
        }

        if (target.matches(".js-source")) {
            syncCover(block);
            return;
        }

        if (target.matches(".js-decision")) {
            block.querySelectorAll(".js-reason-check, .js-sub").forEach(box => {
                box.checked = false;
            });
        }

        if (target.matches(".js-reason-check") && !target.checked) {
            const group = target.closest(".js-reason-group");

            group?.querySelectorAll(".js-sub").forEach(box => {
                box.checked = false;
            });
        }

        if (block.classList.contains("global-block")) {
            refresh(block, !find(block, ".js-on").checked);
            syncCovers(block.dataset.kind);
        } else {
            syncCover(block);
        }
    });

    document.addEventListener("input", event => {
        if (!event.target.matches(".js-global-notes")) return;

        const block = event.target.closest(".global-block");

        if (block) {
            syncCovers(block.dataset.kind);
        }
    });

    Object.keys(globalBlocks).forEach(applyGlobalSwitch);

    document.querySelectorAll(".cover-block").forEach(syncCover);
})();
