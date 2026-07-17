(() => {
    "use strict";

    const form =
        document.getElementById("adminPinForm");

    const pin =
        document.getElementById("pin");

    const confirmPin =
        document.getElementById("confirmPin");

    const message =
        document.getElementById("pinMessage");

    const toggleButtons =
        document.querySelectorAll(".toggle-button");

    function keepDigitsOnly(input) {
        input.value = input.value
            .replace(/\D/g, "")
            .slice(0, 6);
    }

    pin?.addEventListener("input", () => {
        keepDigitsOnly(pin);
    });

    confirmPin?.addEventListener("input", () => {
        keepDigitsOnly(confirmPin);
    });

    toggleButtons.forEach(button => {

        button.addEventListener("click", () => {

            const targetId = button.dataset.target;

            const input =
                document.getElementById(targetId);

            if (!input) return;

            const show =
                input.type === "password";

            input.type =
                show ? "text" : "password";

            button.textContent =
                show ? "Hide" : "Show";

        });

    });

    form?.addEventListener("submit", event => {

        message.hidden = true;
        message.textContent = "";
        message.className = "client-message";

        if (
            pin.value.length !== 6 ||
            confirmPin.value.length !== 6
        ) {
            event.preventDefault();

            message.hidden = false;
            message.textContent =
                "Please enter the complete 6-digit PIN.";

            message.classList.add("error");

            return;
        }

        if (pin.value !== confirmPin.value) {

            event.preventDefault();

            message.hidden = false;
            message.textContent =
                "PIN and Confirm PIN do not match.";

            message.classList.add("error");
        }

    });

})();