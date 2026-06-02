let ws;

// Disconnect websocket from StreamerBot
function disconnectSocket() {
    if (!ws || ws.readyState !== 1) {
        log("WebSocket not connected");
        return;
    }

    ws.close();
    log("disconnected");
}

// Connect websocket to StreamerBot
function connectSocket() {
    if (isConnected) {
        return;
    }

    const IP = document.getElementById("websocket-IP-input").value;
    const port = document.getElementById("websocket-port-input").value;

    ws = new WebSocket(`ws://${IP}:${port}/`);

    // When websocket opens, subscribe to all custom events
    ws.onopen = () => {
        isConnected = true;
        SetCSSConnected(true, "WS-connection-status-circle", "WS-connection-status-text");
        SetButtonEnabled(false, "WS-button-connect");
        SetButtonEnabled(true, "WS-button-disconnect");
        SetButtonEnabled(true, "credentials-button-connect");

        // Subscribe to all custom events
        subscribeEvents();
        // Ask StreamerBot for all available actions
        getActions();
    };

    // Split messages between custom broadcasts and responses from StreamerBot
    ws.onmessage = (event) => {
        const data = JSON.parse(event.data);
        log("Received: " + event.data);

        // Custom broadcasts
        if (data.event?.type === "Custom") {
            const responseData = data.data;
            const requestID = responseData.requestID;
            let value;

            // Find the pending action that initiated the request
            if (pendingEvents.has(requestID)) {
                value = pendingEvents.get(requestID)
            }
            // Pop the event and process it
            pendingEvents.delete(requestID);
            handleEvent(value.action, responseData)
        }

        // Normal events
        else {
            const id = data.id;
            if (data.args) {
                const runningActionId = data.args.runningActionId;
                const actionName = data.args.actionName;

                // Add event to pending events
                pendingEvents.set(runningActionId, {
                    action: actionName,
                    timestamp: Date.now()
                });
                return;
            }

            // Answer to getActions request
            // Get all actions and put them in actions table
            if (actionsRequestID !== undefined && actionsRequestID === id) {
                data.actions.forEach(item => actions.set(item.name, item));

                // Ask StreamerBot if user is logged in
                sendAction("SPOTIFYBOT - Check connection status", undefined)
                return;
            }
        }
    };

    // Handle errors
    ws.onerror = (err) => {
        log("Error");
        console.log(err);
    };

    // Handle closed socket
    ws.onclose = () => {
        // Reset CSS
        SetCSSConnected(false, "WS-connection-status-circle", "WS-connection-status-text");
        SetCSSConnected(false, "API-connection-status-circle", "API-connection-status-text");

        SetButtonEnabled(true, "WS-button-connect");
        SetButtonEnabled(false, "WS-button-disconnect");
        SetButtonEnabled(false, "credentials-button-connect");
        SetButtonEnabled(false, "device-button-update");
        SetButtonEnabled(false, "device-button-refresh");

        isSpotifyConnected = false;
        isConnected = false;
    };
}

// Subscribe to all available events
function subscribeEvents() {
    const uuid = crypto.randomUUID();

    // Subscribe to all custom events
    ws.send(JSON.stringify({
        request: "Subscribe",
        id: uuid,
        events: {
            General: ["Custom"]
        }
    }));
}
// Get all available StreamerBot actions
function getActions() {
    const uuid = crypto.randomUUID();
    ws.send(JSON.stringify({
        request: "GetActions",
        id: uuid
    }));
    actionsRequestID = uuid;
}

// Send an action to StreamerBot
function sendAction(actionName, args) {
    if (!ws || ws.readyState !== 1) {
        console.log("WebSocket not connected");
        return;
    }

    // Get action object and id from the actions table
    if (!actions.has(actionName)) {
        console.log(`Action ${actionName} doesn't exist in actions table`);
        return;
    }
    let value = actions.get(actionName);
    let actionID = value.id;
    if (args === undefined) {
        args = {data: {}};
    }

    // Send the request on websocket with id and args
    const uuid = crypto.randomUUID();
    ws.send(JSON.stringify({
        request: "DoAction",
        action: {
            id: actionID
        },
        args: args,
        id: uuid
    }))
}

