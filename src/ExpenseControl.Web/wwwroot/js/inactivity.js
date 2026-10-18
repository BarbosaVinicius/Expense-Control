// Calls back into .NET once the user has been idle for the configured time.
// Loaded on demand as an ES module by the InactivityMonitor component.

const activityEvents = ["mousemove", "keydown", "click", "scroll", "touchstart"];

let timer = null;
let dotNetRef = null;
let timeoutMs = 0;

function restartTimer() {
    clearTimeout(timer);
    timer = setTimeout(() => dotNetRef?.invokeMethodAsync("OnInactive"), timeoutMs);
}

export function start(reference, timeoutInMilliseconds) {
    stop();
    dotNetRef = reference;
    timeoutMs = timeoutInMilliseconds;
    activityEvents.forEach(name => document.addEventListener(name, restartTimer, { passive: true }));
    restartTimer();
}

export function stop() {
    clearTimeout(timer);
    activityEvents.forEach(name => document.removeEventListener(name, restartTimer));
    dotNetRef = null;
}
