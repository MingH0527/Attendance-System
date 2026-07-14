(() => {
    "use strict";

    const form =
        document.getElementById("forgotPasswordForm");

    const username =
        document.getElementById("username");

    const password =
        document.getElementById("password");

    const confirmPassword =
        document.getElementById("confirmPassword");

    const usernameError =
        document.getElementById("usernameError");

    const passwordError =
        document.getElementById("passwordError");

    const confirmPasswordError =
        document.getElementById("confirmPasswordError");

    const lengthRule =
        document.getElementById("lengthRule");

    const uppercaseRule =
        document.getElementById("uppercaseRule");

    const lowercaseRule =
        document.getElementById("lowercaseRule");

    const numberRule =
        document.getElementById("numberRule");

    const symbolRule =
        document.getElementById("symbolRule");

    if (
        !form ||
        !username ||
        !password ||
        !confirmPassword
    ) {
        return;
    }

    const passwordPattern =
        /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,15}$/;

    function markInput(input, valid) {
        input.classList.remove(
            "input-valid",
            "input-invalid"
        );

        if (input.value.trim() === "") {
            return;
        }

        input.classList.add(
            valid
                ? "input-valid"
                : "input-invalid"
        );
    }

    function updateRule(ruleElement, valid) {
        if (!ruleElement) {
            return;
        }

        const icon =
            ruleElement.querySelector(".rule-icon");

        ruleElement.classList.toggle(
            "rule-valid",
            valid
        );

        ruleElement.classList.toggle(
            "rule-invalid",
            !valid
        );

        if (icon) {
            icon.textContent =
                valid
                    ? "✓"
                    : "○";
        }
    }

    function updatePasswordRules() {
        const value = password.value;

        const lengthValid =
            value.length >= 8 &&
            value.length <= 15;

        const uppercaseValid =
            /[A-Z]/.test(value);

        const lowercaseValid =
            /[a-z]/.test(value);

        const numberValid =
            /\d/.test(value);

        const symbolValid =
            /[@$!%*?&]/.test(value);

        updateRule(
            lengthRule,
            lengthValid
        );

        updateRule(
            uppercaseRule,
            uppercaseValid
        );

        updateRule(
            lowercaseRule,
            lowercaseValid
        );

        updateRule(
            numberRule,
            numberValid
        );

        updateRule(
            symbolRule,
            symbolValid
        );

        return (
            lengthValid &&
            uppercaseValid &&
            lowercaseValid &&
            numberValid &&
            symbolValid
        );
    }

    function validateUsername() {
        const value =
            username.value.trim();

        usernameError.textContent = "";

        if (value === "") {
            usernameError.textContent =
                "Username is required.";

            markInput(username, false);

            return false;
        }

        if (value.length > 30) {
            usernameError.textContent =
                "Username cannot exceed 30 characters.";

            markInput(username, false);

            return false;
        }

        markInput(username, true);

        return true;
    }

    function validatePassword() {
        const value =
            password.value;

        passwordError.textContent = "";

        updatePasswordRules();

        if (value === "") {
            passwordError.textContent =
                "New password is required.";

            markInput(password, false);

            return false;
        }

        if (!passwordPattern.test(value)) {
            passwordError.textContent =
                "Password does not meet all requirements.";

            markInput(password, false);

            return false;
        }

        markInput(password, true);

        return true;
    }

    function validateConfirmPassword() {
        confirmPasswordError.textContent = "";

        if (confirmPassword.value === "") {
            confirmPasswordError.textContent =
                "Please confirm your password.";

            markInput(confirmPassword, false);

            return false;
        }

        if (
            confirmPassword.value !==
            password.value
        ) {
            confirmPasswordError.textContent =
                "Passwords do not match.";

            markInput(confirmPassword, false);

            return false;
        }

        markInput(confirmPassword, true);

        return true;
    }

    username.addEventListener(
        "input",
        validateUsername
    );

    password.addEventListener("input", () => {
        validatePassword();

        if (confirmPassword.value !== "") {
            validateConfirmPassword();
        }
    });

    confirmPassword.addEventListener(
        "input",
        validateConfirmPassword
    );

    document
        .querySelectorAll(
            "[data-password-target]"
        )
        .forEach(button => {
            button.addEventListener(
                "click",
                () => {
                    const targetId =
                        button.dataset.passwordTarget;

                    const targetInput =
                        document.getElementById(
                            targetId
                        );

                    if (!targetInput) {
                        return;
                    }

                    const isVisible =
                        targetInput.type === "text";

                    targetInput.type =
                        isVisible
                            ? "password"
                            : "text";

                    button.textContent =
                        isVisible
                            ? "Show"
                            : "Hide";
                }
            );
        });

    form.addEventListener(
        "submit",
        event => {
            const usernameValid =
                validateUsername();

            const passwordValid =
                validatePassword();

            const confirmPasswordValid =
                validateConfirmPassword();

            if (
                !usernameValid ||
                !passwordValid ||
                !confirmPasswordValid
            ) {
                event.preventDefault();
            }
        }
    );

    updatePasswordRules();
})();