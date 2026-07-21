(() => {
    "use strict";

    const app =
        document.getElementById("chatApp");

    if (!app) {
        return;
    }

    const currentUserId =
        Number(
            app.dataset.currentUserId
        );

    const initialSelectedUserId =
        Number(
            app.dataset.selectedUserId ||
            0
        );

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
        0;

    let selectedConversationId =
        0;

    let selectedUserName =
        "";

    let oldestMessageId =
        0;

    let searchTimer =
        null;

    let isLoadingOlder =
        false;

    let pendingCallerId =
        0;

    let pendingCallerName =
        "";

    let callPeerUserId =
        0;

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

    function messageHtml(
        message) {
        const mine =
            Number(
                message.senderId
            ) ===
            currentUserId;

        return `
            <article
                class="message-row ${
                    mine
                        ? "message-mine"
                        : "message-other"
                }"
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

                        ${
                            mine
                                ? `<span class="read-state">${
                                    message.isRead
                                        ? "Read"
                                        : "Sent"
                                  }</span>`
                                : ""
                        }

                    </div>

                </div>

            </article>`;
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

        oldestMessageId =
            oldestMessageId === 0
                ? Number(
                    message.messageId
                )
                : Math.min(
                    oldestMessageId,

                    Number(
                        message.messageId
                    )
                );

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

        if (messages.length > 0) {
            oldestMessageId =
                Number(
                    messages[0]
                        .messageId
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
            Number(
                otherUserId
            );

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
            .forEach(
                item => {
                    item
                        .classList
                        .toggle(
                            "active",

                            Number(
                                item.dataset.userId
                            ) ===
                            selectedUserId
                        );
                }
            );

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
                Number(
                    data.conversationId
                );

            selectedUserName =
                data.otherUser.username;

            activeChatAvatar.src =
                `/image/${data.otherUser.avatar}`;

            activeChatAvatar.onerror =
                () => {
                    activeChatAvatar.src =
                        "/image/default.png";
                };

            activeChatName.textContent =
                data.otherUser.username;

            activeChatInfo.textContent =
                data.otherUser.position;

            emptyState.hidden =
                true;

            activeChat.hidden =
                false;

            data.messages
                .forEach(
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
                selectedConversationId >
                    0 &&

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

                                <span class="message-action">
                                    ›
                                </span>

                            </button>`
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

                                        ${
                                            item.unreadCount > 0
                                                ? `<span class="unread-badge">
                                                    ${item.unreadCount}
                                                   </span>`
                                                : ""
                                        }

                                    </span>

                                </span>

                            </button>`
                    )
                    .join("");
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
            !navigator.mediaDevices.getUserMedia
        ) {
            throw new Error(
                "This browser does not support camera or microphone access."
            );
        }

        try {
            /*
             * First attempt:
             * camera + microphone.
             */
            localStream =
                await navigator.mediaDevices.getUserMedia({
                    video: {
                        width: {
                            ideal: 1280
                        },

                        height: {
                            ideal: 720
                        },

                        facingMode: "user"
                    },

                    audio: true
                });
        }
        catch (cameraError) {
            console.error(
                "Camera and microphone request failed:",
                cameraError
            );

            /*
             * When Chrome already uses the laptop camera,
             * Edge may fail to open the same physical camera.
             *
             * For development on one computer, continue with
             * audio only instead of rejecting the entire call.
             */
            if (
                cameraError?.name === "NotReadableError" ||
                cameraError?.name === "NotFoundError" ||
                cameraError?.name === "OverconstrainedError"
            ) {
                try {
                    localStream =
                        await navigator.mediaDevices.getUserMedia({
                            video: false,
                            audio: true
                        });

                    cameraEnabled = false;

                    showError(
                        "Camera is unavailable or already in use. " +
                        "The call will continue with audio only."
                    );
                }
                catch (audioError) {
                    console.error(
                        "Audio-only request failed:",
                        audioError
                    );

                    throw audioError;
                }
            }
            else {
                throw cameraError;
            }
        }

        localVideo.srcObject =
            localStream;

        microphoneEnabled =
            localStream.getAudioTracks().length > 0;

        cameraEnabled =
            localStream.getVideoTracks().length > 0;

        /*
         * Some browsers need an explicit play request.
         * The error can be ignored because the element is muted.
         */
        try {
            await localVideo.play();
        }
        catch (playError) {
            console.warn(
                "Local preview could not autoplay:",
                playError
            );
        }

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
            event => {
                const stream =
                    event.streams[0];

                if (stream) {
                    remoteVideo.srcObject =
                        stream;

                    remoteVideoPlaceholder.hidden =
                        true;

                    videoCallStatus.textContent =
                        "Connected";
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
            0;

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

            await connection.invoke(
                "StartVideoCall",
                callPeerUserId
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

        const callerId =
            pendingCallerId;

        const callerName =
            pendingCallerName;

        try {
            callPeerUserId =
                callerId;

            callPeerName =
                callerName;

            hideIncomingCall();

            showVideoCallOverlay(
                callPeerName,
                "Preparing camera and microphone…"
            );

            await ensureLocalMedia();

            createPeerConnection();

            videoCallStatus.textContent =
                "Connecting…";

            remoteVideoPlaceholder.textContent =
                "Connecting…";

            await connection.invoke(
                "AcceptVideoCall",
                callPeerUserId
            );
        }
        catch (error) {
            console.error(
                "Accept call failed:",
                error
            );

            cleanupCall();

            /*
             * Reject only when the receiver genuinely cannot
             * access any usable media device.
             */
            if (
                callerId &&
                connection.state ===
                    signalR.HubConnectionState.Connected
            ) {
                try {
                    await connection.invoke(
                        "RejectVideoCall",
                        callerId
                    );
                }
                catch (rejectError) {
                    console.error(
                        "Reject call notification failed:",
                        rejectError
                    );
                }
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
            Number(
                callerId
            );

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
                    peerUserId
                );
            }
            catch {
                // Local call is already closed.
            }
        }
    }

    function cleanupCall() {
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
            0;

        callPeerName =
            "";

        callIsActive =
            false;

        microphoneEnabled =
            true;

        cameraEnabled =
            true;

        updateMediaButtonStates();
    }

    function getMediaErrorMessage(error) {
        console.error(
            "Media error:",
            error
        );

        if (
            error?.name === "NotAllowedError"
        ) {
            return (
                "Camera or microphone permission was denied. " +
                "Click the camera icon beside the address bar and select Allow."
            );
        }

        if (
            error?.name === "NotFoundError"
        ) {
            return (
                "No available camera or microphone was found."
            );
        }

        if (
            error?.name === "NotReadableError"
        ) {
            return (
                "The camera or microphone is already being used " +
                "by another browser or application."
            );
        }

        if (
            error?.name === "OverconstrainedError"
        ) {
            return (
                "The selected camera does not support the requested settings."
            );
        }

        if (
            error?.name === "SecurityError"
        ) {
            return (
                "Camera access requires HTTPS or localhost."
            );
        }

        return (
            error?.message ||
            "Video call could not be started."
        );
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
                        callPeerUserId
                    )
                    .catch(
                        () => {}
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
                Number(
                    message.senderId
                ) ===
                    selectedUserId ||

                Number(
                    message.receiverId
                ) ===
                    selectedUserId;

            if (belongsToOpenChat) {
                appendMessage(
                    message
                );

                if (
                    Number(
                        message.receiverId
                    ) ===
                        currentUserId &&

                    selectedConversationId >
                        0
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
                Number(
                    conversationId
                ) !==
                    selectedConversationId ||

                Number(
                    readerId
                ) ===
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
            console.log(
                "IncomingVideoCall received:",
                caller
            );
            if (callIsActive) {
                console.log(
                    "Incoming call ignored because this tab is already in a call."
                );

                return;
            }

            pendingCallerId =
                Number(
                    caller.callerId
                );

            pendingCallerName =
                caller.callerName;

            incomingCallerName.textContent =
                caller.callerName;

            incomingCallerAvatar.src =
                `/image/${caller.callerAvatar}`;

            incomingCallerAvatar.onerror =
                () => {
                    incomingCallerAvatar.src =
                        "/image/default.png";
                };

            incomingCallOverlay.hidden =
                false;
        }
    );

    connection.on(
        "VideoCallAccepted",
        async accepterId => {
            console.log(
                "VideoCallAccepted received from:",
                accepterId
            );
            if (
                Number(
                    accepterId
                ) !==
                callPeerUserId
            ) {
                return;
            }

            videoCallStatus.textContent =
                "Connecting…";

            await createAndSendOffer();
        }
    );

    connection.on(
        "VideoCallRejected",
        rejecterId => {
            if (
                Number(
                    rejecterId
                ) !==
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
                Number(
                    answererId
                ) !==
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
                Number(
                    senderId
                ) !==
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
        "VideoCallEnded",
        senderId => {
            if (
                Number(
                    senderId
                ) !==
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

            console.log(
                "SignalR connected.",
                {
                    currentUserId,
                    connectionId:
                        connection.connectionId
                }
            );

            if (
                initialSelectedUserId >
                0
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
    updateMediaButtonStates(); cloudflared--version
    start(); cloudflared--versioncloudflared--version
})();
