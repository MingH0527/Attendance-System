document.addEventListener("DOMContentLoaded", function () {
    const loginForm = document.getElementById("loginForm");
    const usernameInput = document.getElementById("username");
    const passwordInput = document.getElementById("password");
    const passwordToggle = document.getElementById("passwordToggle");
    const loginButton = document.getElementById("loginButton");

    const usernameError = document.getElementById("usernameError");
    const passwordError = document.getElementById("passwordError");

    /*
     * Show or hide password
     */
    passwordToggle.addEventListener("click", function () {
        const passwordIsHidden = passwordInput.type === "password";

        passwordInput.type = passwordIsHidden
            ? "text"
            : "password";

        passwordToggle.textContent = passwordIsHidden
            ? "👁️"
            : "🙈";

        passwordToggle.setAttribute(
            "aria-label",
            passwordIsHidden
                ? "Hide password"
                : "Show password"
        );
    });

    /*
     * Remove error when user starts typing
     */
    usernameInput.addEventListener("input", function () {
        clearInputError(usernameInput, usernameError);
    });

    passwordInput.addEventListener("input", function () {
        clearInputError(passwordInput, passwordError);
    });

    /*
     * Validate before submitting
     */
    loginForm.addEventListener("submit", function (event) {
        let isValid = true;

        clearInputError(usernameInput, usernameError);
        clearInputError(passwordInput, passwordError);

        const username = usernameInput.value.trim();
        const password = passwordInput.value;

        if (username === "") {
            showInputError(
                usernameInput,
                usernameError,
                "Please enter your username."
            );

            isValid = false;
        }

        if (password.trim() === "") {
            showInputError(
                passwordInput,
                passwordError,
                "Please enter your password."
            );

            isValid = false;
        }

        if (!isValid) {
            event.preventDefault();

            const firstInvalidInput =
                loginForm.querySelector(".input-error");

            if (firstInvalidInput) {
                firstInvalidInput.focus();
            }

            return;
        }

        /*
         * Prevent repeated clicks after valid submission.
         */
        loginButton.disabled = true;
        loginButton.textContent = "LOGGING IN...";
    });

    function showInputError(input, errorElement, message) {
        input.classList.add("input-error");
        errorElement.textContent = message;
    }

    function clearInputError(input, errorElement) {
        input.classList.remove("input-error");
        errorElement.textContent = "";
    }
});