(() => {
    "use strict";

    const app =
        document.getElementById("chatApp");

    if (!app) {
        return;
    }

    const currentUserId =
        Number(app.dataset.currentUserId);

    const initialSelectedUserId =
        Number(app.dataset.selectedUserId || 0);

    const searchInput =
        document.getElementById("chatSearchInput");

    const searchResults =
        document.getElementById("chatSearchResults");

    const conversationList =
        document.getElementById("conversationList");

    const conversationCount =
        document.getElementById("conversationCount");

    const emptyState =
        document.getElementById("chatEmptyState");

    const activeChat =
        document.getElementById("activeChat");

    const activeChatAvatar =
        document.getElementById("activeChatAvatar");

    const activeChatName =
        document.getElementById("activeChatName");

    const activeChatInfo =
        document.getElementById("activeChatInfo");

    const messageList =
        document.getElementById("messageList");

    const messageArea =
        document.getElementById("messageArea");

    const messageInput =
        document.getElementById("messageInput");

    const characterCount =
        document.getElementById("characterCount");

    const sendButton =
        document.getElementById("sendMessageButton");

    const chatError =
        document.getElementById("chatError");

    const loadOlderButton =
        document.getElementById("loadOlderButton");

    let selectedUserId = 0;
    let selectedConversationId = 0;
    let oldestMessageId = 0;
    let searchTimer = null;
    let isLoadingOlder = false;

    const connection =
        new signalR.HubConnectionBuilder()
            .withUrl("/chatHub")
            .withAutomaticReconnect()
            .build();

    function escapeHtml(value) {
        const element =
            document.createElement("div");

        element.textContent =
            value ?? "";

        return element.innerHTML;
    }

    /*
     * SQL Server datetime/datetime2 does not preserve DateTime.Kind.
     * A value loaded from EF may therefore arrive without "Z".
     * Without this correction, JavaScript treats it as local time,
     * causing the eight-hour Malaysia time difference after reopening.
     */
    function parseUtcDate(utcValue) {
        if (!utcValue) {
            return null;
        }

        let normalized =
            String(utcValue).trim();

        const hasTimeZone =
            /(?:Z|[+-]\d{2}:\d{2})$/i.test(normalized);

        if (!hasTimeZone) {
            normalized += "Z";
        }

        const date =
            new Date(normalized);

        return Number.isNaN(date.getTime())
            ? null
            : date;
    }

    function formatTime(utcValue) {
        const date =
            parseUtcDate(utcValue);

        if (!date) {
            return "";
        }

        const malaysiaTodayText =
            new Intl.DateTimeFormat(
                "en-MY",
                {
                    timeZone:
                        "Asia/Kuala_Lumpur",

                    year:
                        "numeric",

                    month:
                        "2-digit",

                    day:
                        "2-digit"
                }
            ).format(new Date());

        const malaysiaMessageDateText =
            new Intl.DateTimeFormat(
                "en-MY",
                {
                    timeZone:
                        "Asia/Kuala_Lumpur",

                    year:
                        "numeric",

                    month:
                        "2-digit",

                    day:
                        "2-digit"
                }
            ).format(date);

        if (malaysiaTodayText ===
            malaysiaMessageDateText) {

            return new Intl.DateTimeFormat(
                "en-MY",
                {
                    timeZone:
                        "Asia/Kuala_Lumpur",

                    hour:
                        "2-digit",

                    minute:
                        "2-digit",

                    hour12:
                        true
                }
            ).format(date);
        }

        return new Intl.DateTimeFormat(
            "en-MY",
            {
                timeZone:
                    "Asia/Kuala_Lumpur",

                month:
                    "short",

                day:
                    "numeric"
            }
        ).format(date);
    }

    function refreshVisibleTimes() {
        document
            .querySelectorAll(
                "time[data-utc]"
            )
            .forEach(time => {
                time.textContent =
                    formatTime(
                        time.dataset.utc
                    );
            });
    }

    function showError(message) {
        chatError.textContent =
            message;

        chatError.hidden =
            false;
    }

    function clearError() {
        chatError.textContent =
            "";

        chatError.hidden =
            true;
    }

    function updateComposer() {
        const length =
            messageInput.value.length;

        characterCount.textContent =
            `${length}/400`;

        characterCount.classList.toggle(
            "near-limit",
            length >= 350 &&
            length < 400
        );

        characterCount.classList.toggle(
            "at-limit",
            length >= 400
        );

        sendButton.disabled =
            length === 0 ||
            messageInput.value
                .trim()
                .length === 0;

        messageInput.style.height =
            "auto";

        messageInput.style.height =
            `${Math.min(
                messageInput.scrollHeight,
                150
            )}px`;
    }

    function messageHtml(message) {
        const mine =
            Number(message.senderId) ===
            currentUserId;

        return `
            <article
                class="message-row ${mine
                    ? "message-mine"
                    : "message-other"}"
                data-message-id="${message.messageId}">

                <div class="message-bubble">

                    <div class="message-content">
                        ${escapeHtml(
                            message.content
                        ).replace(
                            /\n/g,
                            "<br>"
                        )}
                    </div>

                    <div class="message-meta">

                        <time
                            data-utc="${escapeHtml(
                                message.sentAtUtc
                            )}">
                            ${formatTime(
                                message.sentAtUtc
                            )}
                        </time>

                        ${mine
                            ? `<span class="read-state">${
                                message.isRead
                                    ? "Read"
                                    : "Sent"
                            }</span>`
                            : ""}

                    </div>

                </div>

            </article>`;
    }

    function appendMessage(
        message,
        scroll = true) {

        const existing =
            document.querySelector(
                `[data-message-id="${message.messageId}"]`
            );

        if (existing) {
            return;
        }

        messageList.insertAdjacentHTML(
            "beforeend",
            messageHtml(message)
        );

        oldestMessageId =
            oldestMessageId === 0
                ? Number(message.messageId)
                : Math.min(
                    oldestMessageId,
                    Number(message.messageId)
                );

        if (scroll) {
            messageArea.scrollTop =
                messageArea.scrollHeight;
        }
    }

    function prependMessages(messages) {
        const oldHeight =
            messageArea.scrollHeight;

        const html =
            messages
                .map(messageHtml)
                .join("");

        messageList.insertAdjacentHTML(
            "afterbegin",
            html
        );

        if (messages.length > 0) {
            oldestMessageId =
                Number(
                    messages[0].messageId
                );
        }

        messageArea.scrollTop =
            messageArea.scrollHeight -
            oldHeight;
    }

    async function openConversation(
        otherUserId) {

        clearError();

        selectedUserId =
            Number(otherUserId);

        selectedConversationId =
            0;

        oldestMessageId =
            0;

        messageList.innerHTML =
            "";

        loadOlderButton.hidden =
            true;

        document
            .querySelectorAll(
                ".conversation-item"
            )
            .forEach(item => {
                item.classList.toggle(
                    "active",

                    Number(
                        item.dataset.userId
                    ) === selectedUserId
                );
            });

        try {
            const response =
                await fetch(
                    `/Chat/GetConversation?otherUserId=${selectedUserId}`
                );

            if (!response.ok) {
                throw new Error(
                    "Unable to open this conversation."
                );
            }

            const data =
                await response.json();

            selectedConversationId =
                Number(data.conversationId);

            activeChatAvatar.src =
                `/image/${data.otherUser.avatar}`;

            activeChatAvatar.onerror =
                () => {
                    activeChatAvatar.src =
                        "/image/default.png";
                };

            activeChatName.textContent =
                data.otherUser.username;

            // Gmail is intentionally hidden.
            activeChatInfo.textContent =
                data.otherUser.position;

            emptyState.hidden =
                true;

            activeChat.hidden =
                false;

            data.messages.forEach(
                message =>
                    appendMessage(
                        message,
                        false
                    )
            );

            loadOlderButton.hidden =
                !data.hasMore;

            messageArea.scrollTop =
                messageArea.scrollHeight;

            if (
                selectedConversationId > 0 &&
                connection.state ===
                    signalR
                        .HubConnectionState
                        .Connected
            ) {
                await connection.invoke(
                    "MarkConversationAsRead",
                    selectedConversationId
                );
            }

            await refreshConversationList();

            messageInput.focus();
        }
        catch (error) {
            showError(
                error.message ||
                "Unable to open conversation."
            );
        }
    }

    async function loadOlderMessages() {
        if (
            isLoadingOlder ||
            !selectedUserId ||
            !oldestMessageId
        ) {
            return;
        }

        isLoadingOlder =
            true;

        loadOlderButton.disabled =
            true;

        try {
            const response =
                await fetch(
                    `/Chat/GetConversation?otherUserId=${selectedUserId}&beforeMessageId=${oldestMessageId}`
                );

            if (!response.ok) {
                throw new Error(
                    "Unable to load older messages."
                );
            }

            const data =
                await response.json();

            prependMessages(
                data.messages
            );

            loadOlderButton.hidden =
                !data.hasMore;
        }
        catch (error) {
            showError(
                error.message ||
                "Unable to load older messages."
            );
        }
        finally {
            isLoadingOlder =
                false;

            loadOlderButton.disabled =
                false;
        }
    }

    async function sendMessage() {
        clearError();

        const content =
            messageInput.value;

        if (
            !selectedUserId ||
            !content.trim()
        ) {
            return;
        }

        if (
            connection.state !==
            signalR
                .HubConnectionState
                .Connected
        ) {
            showError(
                "Real-time connection is not ready. Please wait or refresh the page."
            );

            return;
        }

        sendButton.disabled =
            true;

        try {
            await connection.invoke(
                "SendMessage",
                selectedUserId,
                content
            );

            messageInput.value =
                "";

            updateComposer();
        }
        catch (error) {
            showError(
                error.message ||
                "Message could not be sent."
            );

            updateComposer();
        }
    }

    function renderSearchResults(users) {
        if (users.length === 0) {
            searchResults.innerHTML =
                `<div class="search-empty">
                    No matching name found.
                </div>`;
        }
        else {
            searchResults.innerHTML =
                users
                    .map(user => `
                        <button
                            type="button"
                            class="search-user"
                            data-user-id="${user.userId}">

                            <img
                                src="/image/${escapeHtml(
                                    user.avatar
                                )}"
                                alt="${escapeHtml(
                                    user.username
                                )}"
                                onerror="this.src='/image/default.png'" />

                            <span class="search-user-details">

                                <strong>
                                    ${escapeHtml(
                                        user.username
                                    )}
                                </strong>

                                <small>
                                    ${escapeHtml(
                                        user.position
                                    )}
                                </small>

                            </span>

                            <span
                                class="message-action"
                                aria-hidden="true">
                                ›
                            </span>

                        </button>`)
                    .join("");
        }

        searchResults.hidden =
            false;
    }

    async function searchUsers(query) {
        const keyword =
            query.trim();

        if (!keyword) {
            searchResults.hidden =
                true;

            searchResults.innerHTML =
                "";

            return;
        }

        try {
            const response =
                await fetch(
                    `/Chat/SearchUsers?query=${encodeURIComponent(
                        keyword
                    )}`
                );

            if (!response.ok) {
                throw new Error();
            }

            const users =
                await response.json();

            renderSearchResults(users);
        }
        catch {
            searchResults.innerHTML =
                `<div class="search-empty">
                    Search is temporarily unavailable.
                </div>`;

            searchResults.hidden =
                false;
        }
    }

    async function refreshConversationList() {
        try {
            const response =
                await fetch(
                    "/Chat/ConversationList"
                );

            if (!response.ok) {
                return;
            }

            const items =
                await response.json();

            conversationCount.textContent =
                String(items.length);

            if (items.length === 0) {
                conversationList.innerHTML =
                    `<div class="conversation-empty">
                        No conversations yet.
                    </div>`;

                return;
            }

            conversationList.innerHTML =
                items
                    .map(item => `
                        <button
                            type="button"
                            class="conversation-item ${
                                Number(
                                    item.otherUserId
                                ) ===
                                selectedUserId
                                    ? "active"
                                    : ""
                            }"
                            data-user-id="${item.otherUserId}"
                            data-conversation-id="${item.conversationId}">

                            <img
                                src="/image/${escapeHtml(
                                    item.otherAvatar
                                )}"
                                alt="${escapeHtml(
                                    item.otherUsername
                                )}"
                                onerror="this.src='/image/default.png'" />

                            <span class="conversation-main">

                                <span class="conversation-topline">

                                    <strong>
                                        ${escapeHtml(
                                            item.otherUsername
                                        )}
                                    </strong>

                                    <time
                                        data-utc="${escapeHtml(
                                            item.lastMessageAtUtc
                                        )}">
                                        ${formatTime(
                                            item.lastMessageAtUtc
                                        )}
                                    </time>

                                </span>

                                <span class="conversation-bottomline">

                                    <span class="conversation-preview">
                                        ${escapeHtml(
                                            item.lastMessage
                                        )}
                                    </span>

                                    ${item.unreadCount > 0
                                        ? `<span class="unread-badge">
                                            ${item.unreadCount}
                                           </span>`
                                        : ""}

                                </span>

                            </span>

                        </button>`)
                    .join("");
        }
        catch {
            // Keep the existing list if refreshing fails.
        }
    }

    searchInput.addEventListener(
        "input",
        () => {
            clearTimeout(
                searchTimer
            );

            searchTimer =
                setTimeout(
                    () =>
                        searchUsers(
                            searchInput.value
                        ),
                    220
                );
        }
    );

    searchResults.addEventListener(
        "click",
        event => {
            const button =
                event.target.closest(
                    ".search-user"
                );

            if (!button) {
                return;
            }

            searchResults.hidden =
                true;

            searchInput.value =
                "";

            openConversation(
                Number(
                    button.dataset.userId
                )
            );
        }
    );

    conversationList.addEventListener(
        "click",
        event => {
            const button =
                event.target.closest(
                    ".conversation-item"
                );

            if (!button) {
                return;
            }

            openConversation(
                Number(
                    button.dataset.userId
                )
            );
        }
    );

    document.addEventListener(
        "click",
        event => {
            if (
                !event.target.closest(
                    ".chat-search-wrap"
                )
            ) {
                searchResults.hidden =
                    true;
            }
        }
    );

    messageInput.addEventListener(
        "input",
        updateComposer
    );

    messageInput.addEventListener(
        "keydown",
        event => {
            if (
                event.key === "Enter" &&
                !event.shiftKey
            ) {
                event.preventDefault();
                sendMessage();
            }
        }
    );

    sendButton.addEventListener(
        "click",
        sendMessage
    );

    loadOlderButton.addEventListener(
        "click",
        loadOlderMessages
    );

    connection.on(
        "ReceiveMessage",
        async message => {
            const belongsToOpenChat =
                Number(message.senderId) ===
                    selectedUserId ||
                Number(message.receiverId) ===
                    selectedUserId;

            if (belongsToOpenChat) {
                appendMessage(message);

                if (
                    Number(
                        message.receiverId
                    ) === currentUserId &&
                    selectedConversationId > 0
                ) {
                    await connection.invoke(
                        "MarkConversationAsRead",
                        selectedConversationId
                    );
                }
            }

            await refreshConversationList();
        }
    );

    connection.on(
        "ConversationUpdated",
        refreshConversationList
    );

    connection.on(
        "MessagesRead",
        (
            conversationId,
            readerId
        ) => {
            if (
                Number(conversationId) !==
                selectedConversationId
            ) {
                return;
            }

            if (
                Number(readerId) ===
                currentUserId
            ) {
                return;
            }

            document
                .querySelectorAll(
                    ".message-mine .read-state"
                )
                .forEach(element => {
                    element.textContent =
                        "Read";
                });
        }
    );

    connection.onreconnecting(
        () => {
            showError(
                "Connection interrupted. Reconnecting..."
            );
        }
    );

    connection.onreconnected(
        () => {
            clearError();
            refreshConversationList();
        }
    );

    connection.onclose(
        () => {
            showError(
                "Real-time connection was closed. Refresh the page to reconnect."
            );
        }
    );

    async function start() {
        try {
            await connection.start();

            if (
                initialSelectedUserId > 0
            ) {
                await openConversation(
                    initialSelectedUserId
                );
            }
        }
        catch {
            showError(
                "Real-time connection failed. Refresh the page to reconnect."
            );
        }
    }

    refreshVisibleTimes();
    updateComposer();
    start();
})();
