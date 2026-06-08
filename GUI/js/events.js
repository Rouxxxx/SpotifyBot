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
    sendAction("SPOTIFYBOT - GUI - Get available devices");
    SetButtonEnabled(true, "device-button-refresh");

    const clientID = data.clientID;
    const clientSecret = data.clientSecret;
    SetConnected(clientID, clientSecret);

    sendAction("SPOTIFYBOT - GUI - Get user info")
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
        const username = document.getElementById("user-name");

        img.src = pfp;
        img.style.display = "block";
        username.style.display = "block";
    }
}

// Modify an element if the value exists
function setIfDefined(value, element, accessor)
{
    if (value === undefined ) {
        return;
    }
        
    element[accessor] = value;
}

// Toggle element if the value exists
function toggleIfDefined(value, element)
{
    if (!value) {
        return;
    }

    const isDisabled = element.classList.contains("disabled");
    if ((isDisabled && value) || (!isDisabled && !value))
    {
        element.classList.toggle("disabled");
    }
}

// In GET mode, update UI to match current StreamerBot configuration
function handleSetOptions(actionName, data) {
    const status = data.status;
    const mode = data.mode;
    if (mode == "get")
    {
        // If blank data, return
        if (data.data === undefined) {
            return;
        }
        const dataObj = JSON.parse(data.data);

        // Song request restriction
        const songrequest_restriction_checkbox = document.getElementById("checkbox-songrequest-restriction");
        const songrequest_restriction = document.getElementById("songrequest-restriction");
        toggleIfDefined(dataObj["SR_restriction"], songrequest_restriction);
        setIfDefined(dataObj["SR_restriction"], songrequest_restriction_checkbox, "checked");

        // Song request restriction list
        const songrequest_restriction_follower = document.getElementById("checkbox-restriction-follower");
        const SR_follower = dataObj["SR_restriction_list"] === undefined ? false : dataObj["SR_restriction_list"]["follower"]
        setIfDefined(SR_follower, songrequest_restriction_follower, "checked");
        const songrequest_restriction_subscriber = document.getElementById("checkbox-restriction-subscriber");
        const SR_subscriber = dataObj["SR_restriction_list"] === undefined ? false : dataObj["SR_restriction_list"]["subscriber"]
        setIfDefined(SR_subscriber, songrequest_restriction_follower, "checked");
        const songrequest_restriction_vip = document.getElementById("checkbox-restriction-vip");
        const SR_vip = dataObj["SR_restriction_list"] === undefined ? false : dataObj["SR_restriction_list"]["vip"]
        setIfDefined(SR_vip, songrequest_restriction_vip, "checked");

        
        // Song length
        const song_length_checkbox = document.getElementById("checkbox-song-length");
        const song_length = document.getElementById("input-song-length");
        const SR_length = dataObj["SR_length"];
        setIfDefined((SR_length === undefined) ? undefined : !SR_length, song_length, "disabled");
        setIfDefined(SR_length, song_length_checkbox, "checked");

        // Song length number
        const song_length_number = document.getElementById("input-song-length");
        const song_length_label = document.getElementById("label-song-length");
        const SR_length_number = dataObj["SR_length_number"];
        setIfDefined(SR_length_number, song_length_number, "value");
        setIfDefined(SR_length_number, song_length_label, "textContent");

        // Queue system
        const songrequest_queue = document.getElementById("checkbox-songrequest-queue");
        const SR_queue = dataObj["SR_queue"];
        setIfDefined(SR_queue, songrequest_queue, "checked");

        // Max requests
        const max_requests_checkbox = document.getElementById("checkbox-max-requests");
        const max_requests = document.getElementById("input-max-requests");
        const SR_maxuser = dataObj["SR_maxuser"];
        setIfDefined((SR_maxuser === undefined) ? undefined : !SR_maxuser, max_requests, "disabled");
        setIfDefined(SR_maxuser, max_requests_checkbox, "checked");

        // Max requests number
        const max_requests_number = document.getElementById("input-max-requests");
        const max_requests_label = document.getElementById("label-max-requests");
        const SR_maxuser_number = dataObj["SR_maxuser_number"];
        setIfDefined(SR_maxuser_number, max_requests_number, "value");
        setIfDefined(SR_maxuser_number, max_requests_label, "textContent");

        // Skip songs
        const skip_songs_checkbox = document.getElementById("checkbox-skip-songs");
        const skip_songs = document.getElementById("input-skip-songs");
        const SR_skip = dataObj["SR_skip"];
        setIfDefined((SR_skip === undefined) ? undefined : !SR_skip, skip_songs, "disabled");
        setIfDefined(SR_skip, skip_songs_checkbox, "checked");

        // Skip songs number
        const skip_songs_number = document.getElementById("input-skip-songs");
        const skip_songs_label = document.getElementById("label-skip-songs");
        const SR_skip_number = dataObj["SR_skip_number"];
        setIfDefined(SR_skip_number, skip_songs_number, "value");
        setIfDefined(SR_skip_number, skip_songs_label, "textContent");

        // Spotify links
        const songrequest_spotifylink = document.getElementById("checkbox-songrequest-spotifylink");
        const SR_spotifylink = dataObj["SR_spotifylink"];
        setIfDefined(SR_spotifylink, songrequest_spotifylink, "checked");

        return;
    }
    if (status != 200) {
        return;
    }
    console.log("Options saved");
}

// Handle response from StreamerBot for all supported actions
function handleEvent(actionName, data) {
    switch (actionName) {
        case "TEST":
            log("THIS IS A TEST WITH ID " + data.requestID)
            break;
        case "SPOTIFYBOT - GUI - Check connection status":
            handleConnected(actionName, data);
            break;
        case "SPOTIFYBOT - Login":
            handleConnected(actionName, data);
            break;
        case "SPOTIFYBOT - GUI - Get available devices":
            handleDevices(actionName, data);
            break;
        case "SPOTIFYBOT - GUI - Set saved device":
            refreshDevices();
            break;
        case "SPOTIFYBOT - GUI - Get user info":
            handleUserInfo(actionName, data);
            break;
        case "SPOTIFYBOT - GUI - Configuration":
            handleSetOptions(actionName, data);
            break;
    }
}
