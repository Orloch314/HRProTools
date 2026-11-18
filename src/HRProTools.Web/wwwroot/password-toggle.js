document.addEventListener("click", event => {
    const button = event.target.closest("[data-password-toggle]");
    if (!button) return;

    const input = document.getElementById(button.dataset.passwordTarget);
    if (!input) return;

    const showing = input.type === "text";
    input.type = showing ? "password" : "text";
    button.setAttribute("aria-label", showing ? "Mostrar contraseña" : "Ocultar contraseña");
    button.setAttribute("title", showing ? "Mostrar contraseña" : "Ocultar contraseña");
    button.querySelector("[data-password-show]").hidden = !showing;
    button.querySelector("[data-password-hide]").hidden = showing;
});
