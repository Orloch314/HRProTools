const dialogs = new WeakMap();
export function show(dialog, triggerId, receiver) {
    const opener = document.getElementById(triggerId) || document.activeElement;
    const state = { opener, onKeyDown: event => {
        if (event.key !== "Tab") return;
        const controls = [...dialog.querySelectorAll('button:not([disabled]),a[href],input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])')].filter(element => element.getClientRects().length > 0);
        const first = controls[0], last = controls.at(-1);
        if (!first) { event.preventDefault(); return; }
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }, onClose: () => {
        if (opener?.isConnected) opener.focus({ preventScroll: true });
        receiver.invokeMethodAsync("NotifyClosed");
    }};
    dialogs.set(dialog, state);
    dialog.addEventListener("close", state.onClose);
    dialog.addEventListener("keydown", state.onKeyDown);
    dialog.showModal();
    dialog.querySelector("[data-dialog-close]")?.focus({ preventScroll: true });
}
export function close(dialog) { if (dialog.open) dialog.close(); }
export function dispose(dialog) {
    const state = dialogs.get(dialog);
    if (!state) return;
    dialog.removeEventListener("close", state.onClose);
    dialog.removeEventListener("keydown", state.onKeyDown);
    if (dialog.open) dialog.close();
    if (state.opener?.isConnected) state.opener.focus({ preventScroll: true });
    dialogs.delete(dialog);
}
