document.addEventListener("DOMContentLoaded", () => {
    const firstInvalidInput = document.querySelector(".input-validation-error");

    if (firstInvalidInput instanceof HTMLElement) {
        firstInvalidInput.focus();
    }
});
