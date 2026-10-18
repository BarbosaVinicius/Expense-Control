// Loaded in <head>, before the page paints, so the saved theme is applied without a flash.
// Exposes window.ecPrefs for the Blazor components (theme, language, file download).
(function () {
    "use strict";

    var THEME_KEY = "ec.theme";
    var LANGUAGE_KEY = "ec.lang";
    var root = document.documentElement;

    function read(key) {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    }

    function write(key, value) {
        try { localStorage.setItem(key, value); } catch (e) { /* storage blocked: the choice just won't persist */ }
    }

    function systemTheme() {
        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }

    function currentTheme() {
        return root.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
    }

    function setTheme(theme) {
        theme = theme === "dark" ? "dark" : "light";
        write(THEME_KEY, theme);
        root.setAttribute("data-bs-theme", theme);
        // Charts listen to this to repaint with the new colours.
        document.dispatchEvent(new CustomEvent("themechange", { detail: theme }));
        return theme;
    }

    function htmlLang(code) {
        return code === "pt" ? "pt-BR" : code;
    }

    function detectLanguage() {
        var browser = ((navigator.languages && navigator.languages[0]) || navigator.language || "en").toLowerCase();
        if (browser.indexOf("pt") === 0) { return "pt"; }
        if (browser.indexOf("es") === 0) { return "es"; }
        return "en";
    }

    var saved = read(THEME_KEY);
    root.setAttribute("data-bs-theme", saved === "dark" || saved === "light" ? saved : systemTheme());

    window.ecPrefs = {
        getTheme: currentTheme,
        setTheme: setTheme,
        toggleTheme: function () { return setTheme(currentTheme() === "dark" ? "light" : "dark"); },

        getLanguage: function () {
            var code = read(LANGUAGE_KEY) || detectLanguage();
            root.lang = htmlLang(code);
            return code;
        },
        setLanguage: function (code) {
            write(LANGUAGE_KEY, code);
            root.lang = htmlLang(code);
        },

        // Saves text as a file. The BOM makes Excel read UTF-8 (accents) correctly.
        downloadText: function (fileName, text) {
            var blob = new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var link = document.createElement("a");
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
        }
    };
})();
