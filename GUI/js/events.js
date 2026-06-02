// Edit the page to show API connected
function SetConnected(clientID, clientSecret) {
    SetCSSConnected(true, "API-connection-status-circle", "API-connection-status-text");

    // Edit client ID input
    if (clientID !== undefined) {
        const clientIDInput = document.getElementById("credentials-clientID-input");
        clientIDInput.value = clientID;
    }

    // Edit client secret input
    if (clientSecret !== undefined) {
        const clientSecretInput = document.getElementById("credentials-clientSecret-input");
        clientSecretInput.value = clientSecret;
    }
}

// Handle the response for Check Connected
function handleConnected(actionName, data) {
    const status = data.status;
    if (status === undefined) {
        console.error("Received connection status request with no status code")
        console.error(data);
        return;
    }

    // Handle errors when checking connection
    if (status != 200) {
        const message = data.message;
        console.error(`Error code ${status}\n${message}`)
        return;
    }
    if (isSpotifyConnected) {
        return;
    }

    // If user is connected to spotify, get the devices and user info
    isSpotifyConnected = true;
    sendAction("SPOTIFYBOT - Get available devices");
    SetButtonEnabled(true, "device-button-refresh");

    const clientID = data.clientID;
    const clientSecret = data.clientSecret;
    SetConnected(clientID, clientSecret);

    sendAction("SPOTIFYBOT - Get user info")
}

// Set the UI for saved device and available devices
function handleDevices(actionName, data) {
    const select = document.getElementById("devices-select");
    // Remove all options
    select.innerHTML = "";

    const requestData = data.data;
    const devices = requestData.devices;
    const activeDevice = requestData.active_device;
    const savedDevice = requestData.saved_device;
    var foundSavedDevice = false;

    // If at least 1 device is available, enable the update button
    SetButtonEnabled((devices.length > 0), "device-button-update");
    
    devices.forEach(item => {
        const option = document.createElement("option");
        const isActive = (item.id === activeDevice);
        const isSaved = (item.id === savedDevice);

        const displayText = `[${item.name}] (${item.type})`;
        const text = `${item.name} (${item.type})`;

        if (!foundSavedDevice && isSaved) {
            document.getElementById("saved-device-text").textContent = displayText;
            foundSavedDevice = true;
        }
        if (isActive) {
            document.getElementById("active-device-text").textContent = displayText;
        }

        option.value = item.id;
        option.selected = isSaved;
        option.textContent = text;
        select.appendChild(option);
    });

    // If active device isn't in the list, show its deviceID
    if (!foundSavedDevice && savedDevice) {
        document.getElementById("saved-device-text").textContent = savedDevice;
    }
}

// Set user name + pfp
function handleUserInfo(actionName, data) {
    const status = data.status;
    if (status != 200) {
        return;
    }

    const requestData = data.data;
    const username = requestData.username;
    const pfp = requestData.pfp;

    // Edit username text
    if (username !== undefined) {
        document.getElementById("user-name").textContent = `Username : ${username}`;
    }

    if (pfp !== undefined) {
        const img = document.getElementById("user-pfp");

        img.src = pfp;
        img.style.display = "block";
    }
}

// Handle response from StreamerBot for all supported actions
function handleEvent(actionName, data) {
    switch (actionName) {
        case "TEST":
            log("THIS IS A TEST WITH ID " + data.requestID)
            break;
        case "SPOTIFYBOT - Check connection status":
            handleConnected(actionName, data);
            break;
        case "SPOTIFYBOT - Login":
            handleConnected(actionName, data);
            break;
        case "SPOTIFYBOT - Get available devices":
            handleDevices(actionName, data);
            break;
        case "SPOTIFYBOT - Set saved device":
            refreshDevices();
            break;
        case "SPOTIFYBOT - Get user info":
            handleUserInfo(actionName, data);
            break;
    }
}
