// Default variables
let actionsRequestID;
let actions = new Map();
let pendingEvents = new Map();
let isConnected = false;
let isSpotifyConnected = false;
let showOptions = false;

function log(msg) {
    document.getElementById("log").textContent += msg + "\n";
}

// Edit UI if user is connected or not
function SetCSSConnected(connected, circle, text) {
    const circleDiv = document.getElementById(circle);
    const textDiv = document.getElementById(text);
    if (connected) {
        circleDiv.classList.add("connected");
        textDiv.innerText = "Connected";
    } else {
        circleDiv.classList.remove("connected");
        textDiv.innerText = "Not connected";
    }
}
// Enable or disable a button
function SetButtonEnabled(enabled, button) {
    const buttonElement = document.getElementById(button);
    buttonElement.disabled = (!enabled);
}

// Connect to spotify through StreamerBot
function connectSpotify() {
    const clientID = document.getElementById("credentials-clientID-input").value;
    const clientSecret = document.getElementById("credentials-clientSecret-input").value;

    sendAction("SPOTIFYBOT - Login", {data: {clientID: clientID, clientSecret: clientSecret}});
}

// Refresh the list of user's devices
function refreshDevices() {
    sendAction("SPOTIFYBOT - GUI - Get available devices");
}
// Update user's saved device to play songs on
function updateDevice() {
    const select = document.getElementById("devices-select");
    const value = select.value;

    sendAction("SPOTIFYBOT - GUI - Set saved device", {data: {device_id: value}})
}

function enableOptions() {
    showOptions = !showOptions;
    const mainPageDiv = document.getElementById("page-main");
    const optionsPageDiv = document.getElementById("page-options");

    mainPageDiv.style.display = (showOptions) ? "none" : "block";
    optionsPageDiv.style.display = (showOptions) ? "block" : "none";
}


function handleCheckboxULChange(id) {
    const item = document.getElementById(id);
    item.classList.toggle("disabled");
}

function handleCheckboxInputChange(id) {
    const item = document.getElementById(id);
    item.disabled = !item.disabled;
}

function setSliderValue(value, id) {
    const item = document.getElementById(id);
    item.textContent = value;
}

function sendOptions() {
    const data = {}

    // Song request restriction
    // If checked, save the new list
    const songrequest_restriction = document.getElementById("checkbox-songrequest-restriction").checked;
    data["SR_restriction"] = songrequest_restriction;
    if (songrequest_restriction) {
        data["SR_restriction_list"] = {
            follower: document.getElementById("checkbox-restriction-follower").checked,
            subscriber: document.getElementById("checkbox-restriction-subscriber").checked,
            vip: document.getElementById("checkbox-restriction-vip").checked,
        }
    }

    // Max requests
    // If enabled, save the new number
    const max_requests = document.getElementById("checkbox-max-requests").checked;
    data["SR_maxuser"] = max_requests;
    if (max_requests) {
        data["SR_maxuser_number"] = document.getElementById("input-max-requests").value;
    }

    // Skip songs
    // If enabled, save the new number
    const skip_songs = document.getElementById("checkbox-skip-songs").checked;
    data["SR_skip"] = skip_songs;
    if (skip_songs) {
        data["SR_skip_number"] = document.getElementById("input-skip-songs").value;
    }

    // Song length
    // If enabled, save the new number
    const song_length = document.getElementById("checkbox-song-length").checked;
    data["SR_length"] = song_length;
    if (song_length) {
        data["SR_length_number"] = document.getElementById("input-song-length").value;
    }

    // Spotify links
    const songrequest_spotifylink = document.getElementById("checkbox-songrequest-spotifylink").checked;
    data["SR_spotifylink"] = songrequest_spotifylink;

    sendAction("SPOTIFYBOT - GUI - Set options", {data: data});
}