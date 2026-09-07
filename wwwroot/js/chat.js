(() => {
    "use strict";

    const app =
        document.getElementById("chatApp");

    if (!app) {
        return;
    }

    const currentUserId =
        app.dataset.currentUserId ||
        "";
    const currentUserName =
        app.dataset.currentUserName ||
        "User";

    const searchInput =
        document.getElementById(
            "chatSearchInput"
        );

    const searchResults =
        document.getElementById(
            "chatSearchResults"
        );

    const conversationList =
        document.getElementById(
            "conversationList"
        );

    const conversationCount =
        document.getElementById(
            "conversationCount"
        );

    const emptyState =
        document.getElementById(
            "chatEmptyState"
        );

    const activeChat =
        document.getElementById(
            "activeChat"
        );

    const activeChatAvatar =
        document.getElementById(
            "activeChatAvatar"
        );

    const activeChatAvatarFallback =
        document.getElementById(
            "activeChatAvatarFallback"
        );

    const activeChatName =
        document.getElementById(
            "activeChatName"
        );

    const activeChatInfo =
        document.getElementById(
            "activeChatInfo"
        );

    const messageList =
        document.getElementById(
            "messageList"
        );

    const messageArea =
        document.getElementById(
            "messageArea"
        );

    const messageInput =
        document.getElementById(
            "messageInput"
        );

    const characterCount =
        document.getElementById(
            "characterCount"
        );

    const sendButton =
        document.getElementById(
            "sendMessageButton"
        );

    const chatError =
        document.getElementById(
            "chatError"
        );

    const loadOlderButton =
        document.getElementById(
            "loadOlderButton"
        );

    const startVideoCallButton =
        document.getElementById(
            "startVideoCallButton"
        );

    const incomingCallOverlay =
        document.getElementById(
            "incomingCallOverlay"
        );

    const incomingCallerAvatar =
        document.getElementById(
            "incomingCallerAvatar"
        );

    const incomingCallerAvatarFallback =
        document.getElementById(
            "incomingCallerAvatarFallback"
        );

    const incomingCallerName =
        document.getElementById(
            "incomingCallerName"
        );

    const acceptCallButton =
        document.getElementById(
            "acceptCallButton"
        );

    const declineCallButton =
        document.getElementById(
            "declineCallButton"
        );

    const videoCallOverlay =
        document.getElementById(
            "videoCallOverlay"
        );

    const remoteVideo =
        document.getElementById(
            "remoteVideo"
        );

    const localVideo =
        document.getElementById(
            "localVideo"
        );

    const remoteVideoPlaceholder =
        document.getElementById(
            "remoteVideoPlaceholder"
        );

    const videoCallPersonName =
        document.getElementById(
            "videoCallPersonName"
        );

    const videoCallStatus =
        document.getElementById(
            "videoCallStatus"
        );

    const toggleMicrophoneButton =
        document.getElementById(
            "toggleMicrophoneButton"
        );

    const toggleCameraButton =
        document.getElementById(
            "toggleCameraButton"
        );

    const endCallButton =
        document.getElementById(
            "endCallButton"
        );

    let selectedUserId =
        "";

    let selectedConversationId =
        "";

    let selectedUserName =
        "";

    let selectedUserAvatar =
        "";

    let oldestMessageId =
        "";

    let searchTimer =
        null;

    let isLoadingOlder =
        false;

    let pendingCallerId =
        "";

    let pendingCallerName =
        "";

    let callPeerUserId =
        "";

    let callPeerName =
        "";

    let localStream =
        null;

    let peerConnection =
        null;

    let microphoneEnabled =
        true;

    let cameraEnabled =
        true;

    let callIsActive =
        false;

    let activeCallMessageId =
        "";

    let pendingCallMessageId =
        "";

    let callConnectedAt =
        null;

    let callTimerInterval =
        null;

    const pendingIceCandidates =
        [];

    const rtcConfiguration =
    {
        iceServers:
            [
                {
                    urls:
                        "stun:stun.l.google.com:19302"
                },
                {
                    urls:
                        "stun:stun1.l.google.com:19302"
                }
            ]
    };

    const connection =
        new signalR
            .HubConnectionBuilder()
            .withUrl(
                "/chatHub"
            )
            .withAutomaticReconnect()
            .build();

    // ==================================================
    // COMMON HELPERS
    // ==================================================

    function escapeHtml(value) {
        const element =
            document.createElement(
                "div"
            );

        element.textContent =
            value ?? "";

        return element.innerHTML;
    }

    function hasCustomAvatar(value) {

        const avatar =
            String(value || "").trim();

        return (
            avatar !== "" &&
            avatar !== "default.png" &&
            avatar !== "default-avatar.png"
        );
    }


    function getAvatarUrl(value) {

        const avatar =
            String(value || "").trim();

        if (!hasCustomAvatar(avatar)) {
            return "";
        }

        if (
            avatar.startsWith("/") ||
            avatar.startsWith("http://") ||
            avatar.startsWith("https://")
        ) {
            return avatar;
        }

        return `/image/${avatar}`;
    }

    function getInitial(username) {
        return (
            String(username || "U")
                .trim()
                .charAt(0)
                .toUpperCase() ||
            "U"
        );
    }

    function avatarMarkup(
        avatarValue,
        username,
        wrapperClass = "chat-avatar-wrap",
        fallbackClass = "chat-avatar-fallback",
        imageClass = "chat-avatar-image"
    ) {
        const avatar =
            getAvatarUrl(
                avatarValue
            );

        const initial =
            getInitial(
                username
            );

        return `
            <span class="${wrapperClass}">
                <span class="${fallbackClass}">
                    ${escapeHtml(initial)}
                </span>

                ${avatar
                ? `
                            <img
                                src="${escapeHtml(avatar)}"
                                class="${imageClass}"
                                alt=""
                                onerror="this.style.display='none';" />
                          `
                : ""
            }
            </span>
        `;
    }

    function setAvatarElements(
        image,
        fallback,
        avatarValue,
        username
    ) {
        if (fallback) {
            fallback.textContent =
                getInitial(
                    username
                );
        }

        if (!image) {
            return;
        }

        image.onload = null;
        image.onerror = null;
        image.hidden = true;
        image.style.display = "none";
        image.removeAttribute("src");

        const avatar =
            getAvatarUrl(
                avatarValue
            );

        if (!avatar) {
            return;
        }

        image.onload =
            () => {
                image.hidden = false;
                image.style.display = "block";

                if (image === activeChatAvatar) {
                    syncOpenConversationAvatars();
                }
            };

        image.onerror =
            () => {
                image.hidden = true;
                image.style.display = "none";
            };

        image.src =
            avatar;
    }


    function parseUtcDate(
        utcValue) {
        if (!utcValue) {
            return null;
        }

        let normalized =
            String(
                utcValue
            ).trim();

        const hasTimeZone =
            /(?:Z|[+-]\d{2}:\d{2})$/i
                .test(
                    normalized
                );

        if (!hasTimeZone) {
            normalized +=
                "Z";
        }

        const date =
            new Date(
                normalized
            );

        return Number.isNaN(
            date.getTime()
        )
            ? null
            : date;
    }

    function formatTime(
        utcValue) {
        const date =
            parseUtcDate(
                utcValue
            );

        if (!date) {
            return "";
        }

        const dateFormatter =
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
            );

        const today =
            dateFormatter.format(
                new Date()
            );

        const messageDate =
            dateFormatter.format(
                date
            );

        if (today ===
            messageDate) {
            return new Intl
                .DateTimeFormat(
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
                )
                .format(
                    date
                );
        }

        return new Intl
            .DateTimeFormat(
                "en-MY",
                {
                    timeZone:
                        "Asia/Kuala_Lumpur",

                    month:
                        "short",

                    day:
                        "numeric"
                }
            )
            .format(
                date
            );
    }

    function refreshVisibleTimes() {
        document
            .querySelectorAll(
                "time[data-utc]"
            )
            .forEach(
                time => {
                    time.textContent =
                        formatTime(
                            time.dataset.utc
                        );
                }
            );
    }

    function showError(
        message) {
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

    // ==================================================
    // TEXT CHAT
    // ==================================================

    function updateComposer() {
        const length =
            messageInput
                .value
                .length;

        characterCount.textContent =
            `${length}/400`;

        characterCount
            .classList
            .toggle(
                "near-limit",

                length >= 350 &&
                length < 400
            );

        characterCount
            .classList
            .toggle(
                "at-limit",
                length >= 400
            );

        sendButton.disabled =
            length === 0 ||

            messageInput
                .value
                .trim()
                .length === 0;

        messageInput.style.height =
            "auto";

        messageInput.style.height =
            `${Math.min(
                messageInput
                    .scrollHeight,
                150
            )}px`;
    }

    function formatDuration(totalSeconds) {
        const seconds = Math.max(0, Number(totalSeconds || 0));
        const minutes = Math.floor(seconds / 60);
        const remaining = seconds % 60;
        return `${minutes}:${String(remaining).padStart(2, "0")}`;
    }

    function messageHtml(message) {
        const mine =
            String(message.senderId || "") ===
            currentUserId;

        const isCall =
            (message.messageType || "Text") ===
            "VideoCall";

        const senderName =
            mine
                ? (
                    message.senderName ||
                    currentUserName
                )
                : (
                    selectedUserName ||
                    message.senderName ||
                    "User"
                );

     
        const senderAvatarValue =
            mine
                ? (message.senderAvatar || "")
                : (selectedUserAvatar || "");

        let contentHtml = "";

        if (isCall) {
            const status =
                message.callStatus ||
                "Calling";

            let detail =
                "Calling…";

            if (status === "Connected") {
                detail =
                    "Video call in progress";
            }
            else if (status === "Completed") {
                detail =
                    `Ended · ${formatDuration(
                        message.callDurationSeconds
                    )}`;
            }
            else if (status === "Declined") {
                detail =
                    "Declined";
            }
            else if (status === "Missed") {
                detail =
                    "Missed video call";
            }

            contentHtml = `
                <div class="call-message-card">
                    <div class="call-message-icon">
                        📹
                    </div>

                    <div>
                        <strong>
                            Video call
                        </strong>

                        <div class="call-message-detail">
                            ${escapeHtml(detail)}
                        </div>
                    </div>
                </div>
            `;
        }
        else {
            contentHtml = `
                <div class="message-content">
                    ${escapeHtml(
                message.content || ""
            ).replace(
                /\n/g,
                "<br>"
            )}
                </div>
            `;
        }

        const avatarHtml =
            avatarMarkup(
                senderAvatarValue,
                senderName,
                "message-avatar-wrap",
                "message-avatar-fallback",
                "message-avatar"
            );

        return `
            <article
                class="message-row ${mine
                ? "message-mine"
                : "message-other"
            }"
                data-message-id="${escapeHtml(
                message.messageId || ""
            )}">

                ${mine
                ? ""
                : avatarHtml
            }

                <div class="message-bubble ${isCall
                ? "call-message-bubble"
                : ""
            }">

                    ${contentHtml}

                    <div class="message-meta">
                        <time data-utc="${escapeHtml(
                message.sentAtUtc || ""
            )}">
                            ${formatTime(
                message.sentAtUtc
            )}
                        </time>

                        ${mine
                ? `
                                    <span class="read-state">
                                        ${message.isRead
                    ? "Read"
                    : "Sent"
                }
                                    </span>
                                  `
                : ""
            }
                    </div>
                </div>

                ${mine
                ? avatarHtml
                : ""
            }
            </article>
        `;
    }

    function syncOpenConversationAvatars() {
        const avatar =
            getAvatarUrl(
                selectedUserAvatar
            );

        const initial =
            getInitial(
                selectedUserName
            );

        document
            .querySelectorAll(
                ".message-other .message-avatar-wrap"
            )
            .forEach(
                wrapper => {
                    const fallback =
                        wrapper.querySelector(
                            ".message-avatar-fallback"
                        );

                    if (fallback) {
                        fallback.textContent =
                            initial;
                    }

                    let image =
                        wrapper.querySelector(
                            ".message-avatar"
                        );

                    if (!avatar) {
                        if (image) {
                            image.remove();
                        }

                        return;
                    }

                    if (!image) {
                        image =
                            document.createElement(
                                "img"
                            );

                        image.className =
                            "message-avatar";

                        image.alt = "";

                        wrapper.appendChild(
                            image
                        );
                    }

                    image.onerror =
                        () => {
                            image.style.display =
                                "none";
                        };

                    image.onload =
                        () => {
                            image.style.display =
                                "block";
                        };

                    // Force the exact same latest avatar path for every
                    // incoming message in the open conversation.
                    image.src = avatar;
                }
            );
    }

    function replaceMessage(message) {
        const existing = document.querySelector(
            `[data-message-id="${message.messageId}"]`
        );

        if (!existing) {
            appendMessage(message, false);
            return;
        }

        existing.outerHTML = messageHtml(message);
        refreshVisibleTimes();
    }

    function appendMessage(
        message,
        scroll = true) {
        const existing =
            document
                .querySelector(
                    `[data-message-id="${message.messageId}"]`
                );

        if (existing) {
            return;
        }

        messageList
            .insertAdjacentHTML(
                "beforeend",
                messageHtml(
                    message
                )
            );

        if (
            String(message.senderId || "") !==
            currentUserId
        ) {
            syncOpenConversationAvatars();
        }

        if (!oldestMessageId) {
            oldestMessageId =
                String(message.messageId || "");
        }

        if (scroll) {
            messageArea.scrollTop =
                messageArea.scrollHeight;
        }
    }

    function prependMessages(
        messages) {
        const oldHeight =
            messageArea
                .scrollHeight;

        const html =
            messages
                .map(
                    messageHtml
                )
                .join("");

        messageList
            .insertAdjacentHTML(
                "afterbegin",
                html
            );

        syncOpenConversationAvatars();

        if (messages.length > 0) {
            oldestMessageId =
                String(
                    messages[0]
                        .messageId ||
                    ""
                );
        }

        messageArea.scrollTop =
            messageArea
                .scrollHeight -
            oldHeight;
    }

    async function openConversation(
        otherUserId) {
        clearError();

        selectedUserId =
            String(otherUserId || "");

        selectedConversationId =
            "";

        selectedUserName =
            "";

        selectedUserAvatar =
            "";

        oldestMessageId =
            "";

        messageList.innerHTML =
            "";

        loadOlderButton.hidden =
            true;

        document
            .querySelectorAll(
                ".conversation-item"
            )
            .forEach(
                item => {
                    item
                        .classList
                        .toggle(
                            "active",
                            item.dataset.userId ===
                            selectedUserId
                        );
                }
            );

        try {
            const response =
                await fetch(
                    `/Chat/GetConversation?otherUserId=${encodeURIComponent(
                        selectedUserId
                    )}`,
                    {
                        cache: "no-store"
                    }
                );

            if (!response.ok) {
                throw new Error(
                    "Unable to open this conversation."
                );
            }

            const data =
                await response.json();

            selectedConversationId =
                data.conversationId ||
                "";

            selectedUserName =
                data.otherUser?.username ||
                "User";

            selectedUserAvatar =
                data.otherUser?.avatar ||
                "";

            activeChatName.textContent =
                selectedUserName;

            activeChatInfo.textContent =
                data.otherUser?.position ||
                "";

            setAvatarElements(
                activeChatAvatar,
                activeChatAvatarFallback,
                selectedUserAvatar,
                selectedUserName
            );

            emptyState.hidden =
                true;

            activeChat.hidden =
                false;

            const messages =
                Array.isArray(data.messages)
                    ? data.messages
                    : [];

            messages.forEach(
                message =>
                    appendMessage(
                        message,
                        false
                    )
            );

            // Extra safety: make every received message use exactly the
            // currently selected user's latest avatar.
            syncOpenConversationAvatars();

            loadOlderButton.hidden =
                !data.hasMore;

            messageArea.scrollTop =
                messageArea.scrollHeight;

            if (
                selectedConversationId &&
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
                "Real-time connection is not ready."
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

    function renderSearchResults(
        users) {
        if (users.length === 0) {
            searchResults.innerHTML =
                `<div class="search-empty">
                    No matching name found.
                </div>`;
        }
        else {
            searchResults.innerHTML =
                users
                    .map(
                        user => `
                            <button
                                type="button"
                                class="search-user"
                                data-user-id="${escapeHtml(
                            user.userId
                        )}">

                                ${avatarMarkup(
                            user.avatar,
                            user.username
                        )}

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

                                <span class="message-action">
                                    ›
                                </span>

                            </button>
                        `
                    )
                    .join("");
        }

        searchResults.hidden =
            false;
    }

    async function searchUsers(
        query) {
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

            renderSearchResults(
                await response.json()
            );
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
                String(
                    items.length
                );

            if (items.length === 0) {
                conversationList.innerHTML =
                    `<div class="conversation-empty">
                        No conversations yet.
                    </div>`;

                return;
            }

            conversationList.innerHTML =
                items
                    .map(
                        item => `
                            <button
                                type="button"
                                class="conversation-item ${String(
                            item.otherUserId ||
                            ""
                        ) ===
                                selectedUserId
                                ? "active"
                                : ""
                            }"
                                data-user-id="${escapeHtml(
                                item.otherUserId
                            )}"
                                data-conversation-id="${escapeHtml(
                                item.conversationId
                            )}">

                                ${avatarMarkup(
                                item.otherAvatar,
                                item.otherUsername
                            )}

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
                                ? `
                                                    <span class="unread-badge">
                                                        ${item.unreadCount}
                                                    </span>
                                                  `
                                : ""
                            }

                                    </span>

                                </span>

                            </button>
                        `
                    )
                    .join("");

            refreshVisibleTimes();
        }
        catch {
            // Existing list remains visible.
        }
    }

    // ==================================================
    // WEBRTC VIDEO CALL
    // ==================================================

    async function ensureLocalMedia() {
        if (localStream) {
            return localStream;
        }

        if (
            !navigator.mediaDevices ||
            !navigator.mediaDevices
                .getUserMedia
        ) {
            throw new Error(
                "This browser does not support camera access."
            );
        }

        localStream =
            await navigator
                .mediaDevices
                .getUserMedia(
                    {
                        video:
                        {
                            width:
                            {
                                ideal:
                                    1280
                            },

                            height:
                            {
                                ideal:
                                    720
                            },

                            facingMode:
                                "user"
                        },

                        audio:
                        {
                            echoCancellation: true,
                            noiseSuppression: true,
                            autoGainControl: true
                        }
                    }
                );

        localVideo.srcObject =
            localStream;

        const audioTracks =
            localStream.getAudioTracks();

        if (audioTracks.length === 0) {
            throw new Error(
                "Microphone permission was granted, but no audio track was created."
            );
        }

        console.log(
            "Local audio track:",
            audioTracks[0].label,
            audioTracks[0].enabled,
            audioTracks[0].readyState
        );

        microphoneEnabled =
            true;

        cameraEnabled =
            true;

        updateMediaButtonStates();

        return localStream;
    }

    function createPeerConnection() {
        if (peerConnection) {
            return peerConnection;
        }

        peerConnection =
            new RTCPeerConnection(
                rtcConfiguration
            );

        peerConnection.onicecandidate =
            async event => {
                if (
                    !event.candidate ||
                    !callPeerUserId
                ) {
                    return;
                }

                await connection.invoke(
                    "SendIceCandidate",
                    callPeerUserId,
                    JSON.stringify(
                        event.candidate
                    )
                );
            };

        peerConnection.ontrack =
            async event => {
                const stream =
                    event.streams[0];

                console.log(
                    "Remote track:",
                    event.track.kind,
                    event.track.enabled,
                    event.track.readyState
                );

                if (stream) {
                    remoteVideo.srcObject =
                        stream;

                    remoteVideo.muted =
                        false;

                    remoteVideo.volume =
                        1;

                    try {
                        await remoteVideo.play();
                    }
                    catch (error) {
                        console.warn(
                            "Remote media autoplay was blocked:",
                            error
                        );
                    }

                    remoteVideoPlaceholder.hidden =
                        true;

                    videoCallStatus.textContent =
                        "Connected";

                    startCallTimer();
                }
            };

        peerConnection.onconnectionstatechange =
            () => {
                const state =
                    peerConnection
                        ?.connectionState;

                if (
                    state ===
                    "connected"
                ) {
                    videoCallStatus.textContent =
                        "Connected";

                    remoteVideoPlaceholder.hidden =
                        true;
                }

                if (
                    state ===
                    "failed" ||

                    state ===
                    "disconnected"
                ) {
                    videoCallStatus.textContent =
                        "Connection interrupted";
                }

                if (
                    state ===
                    "closed"
                ) {
                    cleanupCall();
                }
            };

        if (localStream) {
            localStream
                .getTracks()
                .forEach(
                    track => {
                        peerConnection
                            .addTrack(
                                track,
                                localStream
                            );
                    }
                );
        }

        return peerConnection;
    }

    async function flushPendingIceCandidates() {
        if (
            !peerConnection ||
            !peerConnection
                .remoteDescription
        ) {
            return;
        }

        while (
            pendingIceCandidates
                .length > 0
        ) {
            const candidate =
                pendingIceCandidates
                    .shift();

            await peerConnection
                .addIceCandidate(
                    candidate
                );
        }
    }

    function startCallTimer() {
        if (callTimerInterval) {
            return;
        }

        callConnectedAt = Date.now();
        const timer = document.getElementById("videoCallTimer");

        const update = () => {
            if (!timer || !callConnectedAt) {
                return;
            }

            const seconds = Math.floor((Date.now() - callConnectedAt) / 1000);
            timer.textContent = formatDuration(seconds);
        };

        update();
        callTimerInterval = setInterval(update, 1000);
    }

    function stopCallTimer() {
        if (callTimerInterval) {
            clearInterval(callTimerInterval);
            callTimerInterval = null;
        }

        callConnectedAt = null;

        const timer = document.getElementById("videoCallTimer");
        if (timer) {
            timer.textContent = "00:00";
        }
    }

    function showVideoCallOverlay(
        peerName,
        status) {
        callPeerName =
            peerName ||
            "Video call";

        videoCallPersonName.textContent =
            callPeerName;

        videoCallStatus.textContent =
            status;

        remoteVideoPlaceholder.hidden =
            false;

        remoteVideoPlaceholder.textContent =
            status;

        videoCallOverlay.hidden =
            false;

        callIsActive =
            true;
    }

    function hideIncomingCall() {
        incomingCallOverlay.hidden =
            true;

        pendingCallerId =
            "";

        pendingCallerName =
            "";
    }

    async function startOutgoingVideoCall() {
        clearError();

        if (!selectedUserId) {
            showError(
                "Select a person before starting a call."
            );

            return;
        }

        if (callIsActive) {
            return;
        }

        try {
            callPeerUserId =
                selectedUserId;

            callPeerName =
                selectedUserName;

            await ensureLocalMedia();

            createPeerConnection();

            showVideoCallOverlay(
                callPeerName,
                "Calling…"
            );

            activeCallMessageId =
                String(
                    await connection.invoke(
                        "StartVideoCall",
                        callPeerUserId
                    )
                );
        }
        catch (error) {
            cleanupCall();

            showError(
                getMediaErrorMessage(
                    error
                )
            );
        }
    }

    async function acceptIncomingCall() {
        if (!pendingCallerId) {
            return;
        }

        try {
            callPeerUserId =
                pendingCallerId;

            callPeerName =
                pendingCallerName;

            hideIncomingCall();

            await ensureLocalMedia();

            createPeerConnection();

            showVideoCallOverlay(
                callPeerName,
                "Connecting…"
            );

            activeCallMessageId =
                pendingCallMessageId;

            await connection.invoke(
                "AcceptVideoCall",
                activeCallMessageId,
                callPeerUserId
            );
        }
        catch (error) {
            const callerId =
                callPeerUserId;

            cleanupCall();

            if (callerId) {
                await connection.invoke(
                    "RejectVideoCall",
                    activeCallMessageId || pendingCallMessageId,
                    callerId
                );
            }

            showError(
                getMediaErrorMessage(
                    error
                )
            );
        }
    }

    async function declineIncomingCall() {
        const callerId =
            pendingCallerId;

        hideIncomingCall();

        if (
            callerId &&
            connection.state ===
            signalR
                .HubConnectionState
                .Connected
        ) {
            await connection.invoke(
                "RejectVideoCall",
                pendingCallMessageId,
                callerId
            );
        }
    }

    async function createAndSendOffer() {
        const pc =
            createPeerConnection();

        const offer =
            await pc.createOffer();

        await pc.setLocalDescription(
            offer
        );

        await connection.invoke(
            "SendWebRtcOffer",
            callPeerUserId,
            JSON.stringify(
                pc.localDescription
            )
        );
    }

    async function receiveOffer(
        callerId,
        offerJson) {
        callPeerUserId =
            String(callerId || "");

        if (!localStream) {
            await ensureLocalMedia();
        }

        const pc =
            createPeerConnection();

        const offer =
            JSON.parse(
                offerJson
            );

        await pc.setRemoteDescription(
            new RTCSessionDescription(
                offer
            )
        );

        await flushPendingIceCandidates();

        const answer =
            await pc.createAnswer();

        await pc.setLocalDescription(
            answer
        );

        await connection.invoke(
            "SendWebRtcAnswer",
            callPeerUserId,
            JSON.stringify(
                pc.localDescription
            )
        );
    }

    async function receiveAnswer(
        answerJson) {
        if (!peerConnection) {
            return;
        }

        const answer =
            JSON.parse(
                answerJson
            );

        await peerConnection
            .setRemoteDescription(
                new RTCSessionDescription(
                    answer
                )
            );

        await flushPendingIceCandidates();
    }

    async function receiveIceCandidate(
        candidateJson) {
        const candidate =
            new RTCIceCandidate(
                JSON.parse(
                    candidateJson
                )
            );

        if (
            peerConnection &&
            peerConnection
                .remoteDescription
        ) {
            await peerConnection
                .addIceCandidate(
                    candidate
                );

            return;
        }

        pendingIceCandidates.push(
            candidate
        );
    }

    function toggleMicrophone() {
        if (!localStream) {
            return;
        }

        microphoneEnabled =
            !microphoneEnabled;

        localStream
            .getAudioTracks()
            .forEach(
                track => {
                    track.enabled =
                        microphoneEnabled;
                }
            );

        updateMediaButtonStates();
    }

    function toggleCamera() {
        if (!localStream) {
            return;
        }

        cameraEnabled =
            !cameraEnabled;

        localStream
            .getVideoTracks()
            .forEach(
                track => {
                    track.enabled =
                        cameraEnabled;
                }
            );

        updateMediaButtonStates();
    }

    function updateMediaButtonStates() {
        toggleMicrophoneButton
            .classList
            .toggle(
                "disabled-media",
                !microphoneEnabled
            );

        toggleCameraButton
            .classList
            .toggle(
                "disabled-media",
                !cameraEnabled
            );

        toggleMicrophoneButton.title =
            microphoneEnabled
                ? "Mute microphone"
                : "Unmute microphone";

        toggleCameraButton.title =
            cameraEnabled
                ? "Turn camera off"
                : "Turn camera on";
    }

    async function endVideoCall(
        notifyOtherUser = true) {
        const peerUserId =
            callPeerUserId;

        const callMessageId =
            activeCallMessageId;

        cleanupCall();

        if (
            notifyOtherUser &&
            peerUserId &&
            connection.state ===
            signalR
                .HubConnectionState
                .Connected
        ) {
            try {
                await connection.invoke(
                    "EndVideoCall",
                    callMessageId,
                    peerUserId
                );
            }
            catch {
                // Local call is already closed.
            }
        }
    }

    function cleanupCall() {
        stopCallTimer();

        if (peerConnection) {
            peerConnection
                .onicecandidate =
                null;

            peerConnection
                .ontrack =
                null;

            peerConnection
                .onconnectionstatechange =
                null;

            peerConnection.close();

            peerConnection =
                null;
        }

        if (localStream) {
            localStream
                .getTracks()
                .forEach(
                    track =>
                        track.stop()
                );

            localStream =
                null;
        }

        localVideo.srcObject =
            null;

        remoteVideo.srcObject =
            null;

        videoCallOverlay.hidden =
            true;

        remoteVideoPlaceholder.hidden =
            false;

        pendingIceCandidates.length =
            0;

        callPeerUserId =
            "";

        callPeerName =
            "";

        callIsActive =
            false;

        activeCallMessageId =
            "";

        pendingCallMessageId =
            "";

        microphoneEnabled =
            true;

        cameraEnabled =
            true;

        updateMediaButtonStates();
    }

    function getMediaErrorMessage(
        error) {
        if (
            error?.name ===
            "NotAllowedError"
        ) {
            return "Camera or microphone permission was denied.";
        }

        if (
            error?.name ===
            "NotFoundError"
        ) {
            return "No camera or microphone was found.";
        }

        if (
            error?.name ===
            "NotReadableError"
        ) {
            return "The camera or microphone is being used by another application.";
        }

        return error?.message ||
            "Video call could not be started.";
    }

    // ==================================================
    // DOM EVENTS
    // ==================================================

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
                button.dataset.userId
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
                button.dataset.userId
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

    startVideoCallButton.addEventListener(
        "click",
        startOutgoingVideoCall
    );

    acceptCallButton.addEventListener(
        "click",
        acceptIncomingCall
    );

    declineCallButton.addEventListener(
        "click",
        declineIncomingCall
    );

    toggleMicrophoneButton.addEventListener(
        "click",
        toggleMicrophone
    );

    toggleCameraButton.addEventListener(
        "click",
        toggleCamera
    );

    endCallButton.addEventListener(
        "click",
        () =>
            endVideoCall(
                true
            )
    );

    window.addEventListener(
        "beforeunload",
        () => {
            if (callPeerUserId) {
                connection
                    .invoke(
                        "EndVideoCall",
                        activeCallMessageId,
                        callPeerUserId
                    )
                    .catch(
                        () => { }
                    );
            }

            cleanupCall();
        }
    );

    // ==================================================
    // SIGNALR EVENTS
    // ==================================================

    connection.on(
        "ReceiveMessage",
        async message => {
            const belongsToOpenChat =
                String(message.senderId || "") ===
                selectedUserId ||

                String(message.receiverId || "") ===
                selectedUserId;

            if (belongsToOpenChat) {
                appendMessage(
                    message
                );

                if (
                    String(message.receiverId || "") ===
                    currentUserId &&

                    selectedConversationId
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
                String(conversationId || "") !==
                selectedConversationId ||

                String(readerId || "") ===
                currentUserId
            ) {
                return;
            }

            document
                .querySelectorAll(
                    ".message-mine .read-state"
                )
                .forEach(
                    element => {
                        element.textContent =
                            "Read";
                    }
                );
        }
    );

    connection.on(
        "IncomingVideoCall",
        caller => {
            if (callIsActive) {
                connection
                    .invoke(
                        "RejectVideoCall",
                        String(caller.callMessageId || ""),
                        String(caller.callerId || "")
                    )
                    .catch(
                        () => { }
                    );

                return;
            }

            pendingCallerId =
                String(caller.callerId || "");

            pendingCallMessageId =
                String(caller.callMessageId || "");

            pendingCallerName =
                caller.callerName ||
                "User";

            incomingCallerName.textContent =
                pendingCallerName;

            setAvatarElements(
                incomingCallerAvatar,
                incomingCallerAvatarFallback,
                caller.callerAvatar || "",
                pendingCallerName
            );

            incomingCallOverlay.hidden =
                false;
        }
    );

    connection.on(
        "VideoCallAccepted",
        async (
            accepterId,
            callMessageId
        ) => {
            if (
                String(accepterId || "") !==
                callPeerUserId
            ) {
                return;
            }

            activeCallMessageId =
                String(callMessageId || "");

            videoCallStatus.textContent =
                "Connecting…";

            await createAndSendOffer();
        }
    );

    connection.on(
        "VideoCallRejected",
        rejecterId => {
            if (
                String(rejecterId || "") !==
                callPeerUserId
            ) {
                return;
            }

            cleanupCall();

            showError(
                "The video call was declined."
            );
        }
    );

    connection.on(
        "ReceiveWebRtcOffer",
        async (
            callerId,
            offerJson
        ) => {
            try {
                await receiveOffer(
                    callerId,
                    offerJson
                );
            }
            catch (error) {
                showError(
                    error.message ||
                    "Could not receive video call."
                );

                endVideoCall(
                    true
                );
            }
        }
    );

    connection.on(
        "ReceiveWebRtcAnswer",
        async (
            answererId,
            answerJson
        ) => {
            if (
                String(answererId || "") !==
                callPeerUserId
            ) {
                return;
            }

            try {
                await receiveAnswer(
                    answerJson
                );
            }
            catch (error) {
                showError(
                    error.message ||
                    "Could not connect the video call."
                );

                endVideoCall(
                    true
                );
            }
        }
    );

    connection.on(
        "ReceiveIceCandidate",
        async (
            senderId,
            candidateJson
        ) => {
            if (
                String(senderId || "") !==
                callPeerUserId
            ) {
                return;
            }

            try {
                await receiveIceCandidate(
                    candidateJson
                );
            }
            catch {
                // Some late ICE candidates can safely be ignored.
            }
        }
    );

    connection.on(
        "CallRecordUpdated",
        async message => {
            replaceMessage(message);
            await refreshConversationList();
        }
    );

    connection.on(
        "VideoCallEnded",
        (
            senderId,
            callMessageId
        ) => {
            if (
                String(senderId || "") !==
                callPeerUserId
            ) {
                return;
            }

            cleanupCall();

            showError(
                "The video call ended."
            );
        }
    );

    connection.onreconnecting(
        () => {
            showError(
                "Connection interrupted. Reconnecting…"
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
            cleanupCall();

            showError(
                "Real-time connection was closed. Refresh the page."
            );
        }
    );

    async function start() {
        try {
            await connection.start();

            if (
                initialSelectedUserId
            ) {
                await openConversation(
                    initialSelectedUserId
                );
            }
        }
        catch {
            showError(
                "Real-time connection failed. Refresh the page."
            );
        }
    }

    refreshVisibleTimes();
    updateComposer();
    updateMediaButtonStates();
    start();
})();
