// Default variables
let actionsRequestID;
let actions = new Map();
let pendingEvents = new Map();
let isConnected = false;
let isSpotifyConnected = false;

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