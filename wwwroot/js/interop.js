﻿window.elementInterop = {
    blurElementById: function (id) {
        document.getElementById(id)?.blur();
    },
    emptyElementById: function (prefix, count) {
        for (let i = 0; i < count; i++) {
            let el = document.getElementById(prefix + i);
            if (el) {
                el.value = ""
            }
        }
    }
};

// Called synchronously from the "Starte App" click so the browser ties audio-autoplay
// permission to that click itself, instead of the later (async, IndexedDB-delayed) first
// play() call - which otherwise happens too far removed from the gesture for some browsers
// to still allow it. Primes the *real*, persistently-mounted #audioPlayer element (see
// MainLayout.razor) rather than a detached dummy, so the gesture-linkage is tied to the
// same element that will actually play syllable audio afterward.
// jsSID tunes loop forever; it only offers a callback once the current tune has played a given
// number of seconds (paused time doesn't count). Reset by every load, so set it per tune.
window.k4l_sidOnEnd = function (dotnetRef, seconds) {
    window.sidPlayer.setendcallback(function () {
        dotnetRef.invokeMethodAsync("OnSidEnded");
    }, seconds);
};

// Tablets and phones: the primary pointer is a finger. Laptops with a touch screen still report a mouse.
window.k4l_isTouchDevice = function () {
    return window.matchMedia("(pointer: coarse)").matches;
};

window.k4l_primeAudioPlayer = function (id) {
    var el = document.getElementById(id);
    if (!el) return;
    var playPromise = el.play();
    if (playPromise) playPromise.catch(function () {}).finally(function () {
        // By the time this settles, k4l_playAudioFile may already have loaded and started the
        // real audio (currentSrc is only non-empty once a real source has loaded) - don't pause
        // that just because the priming call is done.
        if (!el.currentSrc) el.pause();
    });
};

// Sets the src on a shared audio element (MainLayout.razor) and plays it; called through
// AudioStatusService. Resolves to false when the clip can't be loaded or played (no connection,
// missing file) or hasn't started within K4L_AUDIO_START_MS. A play() the browser blocks for
// autoplay, or one cut short by the next src, still counts as playable.
var K4L_AUDIO_START_MS = 8000;
window.k4l_playAudioFile = function (id, src) {
    var el = document.getElementById(id);
    if (!el) return Promise.resolve(true);
    el.src = src;
    var started = Promise.resolve(el.play()).then(function () {
        return true;
    }, function (e) {
        return !!e && (e.name === "NotAllowedError" || e.name === "AbortError");
    });
    var timeout = new Promise(function (resolve) {
        setTimeout(function () { resolve(false); }, K4L_AUDIO_START_MS);
    });
    return Promise.race([started, timeout]);
};

// Whether the app's own server answers: a HEAD request for a file it always has, past the cache.
window.k4l_probeServer = async function (url, timeoutMs) {
    var ctrl = new AbortController();
    var timer = setTimeout(function () { ctrl.abort(); }, timeoutMs);
    try {
        var response = await fetch(url, { method: "HEAD", cache: "no-store", signal: ctrl.signal });
        return response.ok;
    } catch (e) {
        return false;
    } finally {
        clearTimeout(timer);
    }
};

// Forwards the browser's offline/online events to AudioStatusService; returns navigator.onLine.
window.k4l_watchConnection = function (dotnetRef) {
    window.addEventListener("offline", function () { dotnetRef.invokeMethodAsync("OnOffline"); });
    window.addEventListener("online", function () { dotnetRef.invokeMethodAsync("OnOnline"); });
    return navigator.onLine;
};

// Shared AudioContext for synthesized sound effects (currently just the Silbenhammer clang -
// no audio file needed). Created/resumed synchronously from the same "Starte App" click gesture
// as k4l_primeAudioPlayer above, since Safari/iOS in particular require an AudioContext to be
// created or resumed inside a real user-gesture call stack, not merely "soon after" it.
window.k4l_primeAudioContext = function () {
    if (!window.__k4lAudioCtx) {
        var Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return;
        window.__k4lAudioCtx = new Ctx();
    }
    if (window.__k4lAudioCtx.state === "suspended") window.__k4lAudioCtx.resume();
};

// Synthesized "hammered into hot iron" clang for Silbenhammer - no audio file: a low
// sine/triangle "thud" (fast attack, ~220ms decay) layered with a short, low-passed noise burst
// for the metallic component. No-op if the AudioContext was never primed (e.g. StartApp() hasn't
// run yet).
window.k4l_playHammerClang = function () {
    var ctx = window.__k4lAudioCtx;
    if (!ctx) return;
    var now = ctx.currentTime;

    var master = ctx.createGain();
    master.gain.value = 0.9;
    master.connect(ctx.destination);

    // Thud
    var osc = ctx.createOscillator();
    osc.type = "triangle";
    osc.frequency.setValueAtTime(110, now);
    osc.frequency.exponentialRampToValueAtTime(70, now + 0.15);
    var thudGain = ctx.createGain();
    thudGain.gain.setValueAtTime(0.0001, now);
    thudGain.gain.exponentialRampToValueAtTime(1.0, now + 0.005);
    thudGain.gain.exponentialRampToValueAtTime(0.001, now + 0.22);
    osc.connect(thudGain).connect(master);
    osc.start(now);
    osc.stop(now + 0.25);

    // Metallic noise burst
    var bufferSize = Math.floor(ctx.sampleRate * 0.15);
    var buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
    var data = buffer.getChannelData(0);
    for (var i = 0; i < bufferSize; i++) data[i] = Math.random() * 2 - 1;
    var noise = ctx.createBufferSource();
    noise.buffer = buffer;
    var lowpass = ctx.createBiquadFilter();
    lowpass.type = "lowpass";
    lowpass.frequency.value = 1000;
    var noiseGain = ctx.createGain();
    noiseGain.gain.setValueAtTime(0.0001, now);
    noiseGain.gain.exponentialRampToValueAtTime(0.6, now + 0.005);
    noiseGain.gain.exponentialRampToValueAtTime(0.001, now + 0.15);
    noise.connect(lowpass).connect(noiseGain).connect(master);
    noise.start(now);
};
// Opt-in session telemetry (see Services/TelemetryService.cs). Only devices that opened the app
// once with ?telemetrie=<url> have an endpoint in localStorage; everywhere else these functions
// do nothing and send nothing. The receiving side is tools/telemetry_receiver.py.
var K4L_TELEMETRY_KEY = "k4l_telemetry_endpoint";
var K4L_TELEMETRY_PENDING_KEY = "k4l_telemetry_pending";
var K4L_TELEMETRY_MAX_PENDING = 30;

function k4l_telemetryEndpoint() {
    try { return localStorage.getItem(K4L_TELEMETRY_KEY); } catch (e) { return null; }
}

function k4l_telemetryPending() {
    try { return JSON.parse(localStorage.getItem(K4L_TELEMETRY_PENDING_KEY) || "{}"); } catch (e) { return {}; }
}

function k4l_telemetrySavePending(pending) {
    var ids = Object.keys(pending).sort();
    while (ids.length > K4L_TELEMETRY_MAX_PENDING) delete pending[ids.shift()];
    try { localStorage.setItem(K4L_TELEMETRY_PENDING_KEY, JSON.stringify(pending)); } catch (e) { }
}

window.k4l_telemetryConfigure = function (value) {
    try {
        if (value === "off") {
            localStorage.removeItem(K4L_TELEMETRY_KEY);
            localStorage.removeItem(K4L_TELEMETRY_PENDING_KEY);
            console.info("[telemetry] disabled on this device");
        } else if (/^https?:\/\//.test(value)) {
            localStorage.setItem(K4L_TELEMETRY_KEY, value);
            console.info("[telemetry] enabled on this device, endpoint " + value);
        } else {
            console.warn("[telemetry] ignoring ?telemetrie=" + value + " (expected a URL or 'off')");
        }
    } catch (e) {
        console.warn("[telemetry] could not store setting", e);
    }
};

// Logs every attempt on every device, opted in or not, so a field test can follow along in the
// console; copy(k4l_telemetrySession) copies the whole session so far as JSON.
window.k4l_telemetryLog = function (entryJson, sessionJson) {
    try {
        var e = JSON.parse(entryJson);
        window.k4l_telemetrySession = JSON.parse(sessionJson);
        console.info("[telemetry] " + (window.k4l_telemetrySession.koop ? "[KOOP] " : "") + (e.correct ? "richtig" : "falsch") + " " + e.view + " L" + e.level + " " +
            e.difficulty + " #" + e.attempt + " " + e.durationMs + " ms " + e.points + " P" +
            (e.detail ? " - " + e.detail : ""), e);
    } catch (err) {
        console.warn("[telemetry] could not log entry", err);
    }
};

window.k4l_telemetryEnabled = function () {
    return !!k4l_telemetryEndpoint();
};

async function k4l_telemetryPost(endpoint, body, timeoutMs) {
    var controller = new AbortController();
    var timer = setTimeout(function () { controller.abort(); }, timeoutMs);
    try {
        // text/plain keeps this a CORS "simple request" (no preflight).
        var response = await fetch(endpoint, {
            method: "POST",
            headers: { "Content-Type": "text/plain" },
            body: body,
            signal: controller.signal
        });
        if (!response.ok) throw new Error("HTTP " + response.status);
        return true;
    } finally {
        clearTimeout(timer);
    }
}

// Sends every pending session (oldest first); returns true only if all of them arrived. Overlapping
// calls are fine: a session is only dropped from the pending list if its stored body is still the
// one that was just delivered.
window.k4l_telemetrySendPending = async function (timeoutMs) {
    var endpoint = k4l_telemetryEndpoint();
    if (!endpoint) return true;
    var pending = k4l_telemetryPending();
    var ids = Object.keys(pending).sort();
    for (var i = 0; i < ids.length; i++) {
        var body = pending[ids[i]];
        try {
            await k4l_telemetryPost(endpoint, body, timeoutMs);
        } catch (e) {
            console.warn("[telemetry] upload to " + endpoint + " failed: " + (e && e.name === "AbortError" ? "timeout after " + timeoutMs + " ms" : e));
            return false;
        }
        var fresh = k4l_telemetryPending();
        if (fresh[ids[i]] === body) {
            delete fresh[ids[i]];
            k4l_telemetrySavePending(fresh);
        }
    }
    return true;
};

window.k4l_telemetrySend = async function (sessionId, body, timeoutMs) {
    if (!k4l_telemetryEndpoint()) return true;
    var pending = k4l_telemetryPending();
    pending[sessionId] = body;
    k4l_telemetrySavePending(pending);
    return await window.k4l_telemetrySendPending(timeoutMs);
};
