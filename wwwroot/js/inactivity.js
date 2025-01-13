let inactivityTimer;
let dotNetHelper;

function resetInactivityTimer() {
    clearTimeout(inactivityTimer);
    inactivityTimer = setTimeout(() => {
        if (dotNetHelper) {
            dotNetHelper.invokeMethodAsync("OnInactivityTimeout");
        }
    }, window.inactivityTimeoutDuration);
}

function startInactivityTimer(dotNetInstance, timeoutDuration) {
    window.inactivityTimeoutDuration = timeoutDuration;
    dotNetHelper = dotNetInstance;

    // Eventos para detectar atividade do usuário
    document.addEventListener("mousemove", resetInactivityTimer);
    document.addEventListener("keydown", resetInactivityTimer);
    document.addEventListener("click", resetInactivityTimer);

    // Inicializa o temporizador
    resetInactivityTimer();
}
