window.elementInterop = {
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

// Sets the src on the shared #audioPlayer element and plays it. Used by challenge pages
// instead of a Blazor src="@_currentAudio" binding, since the element itself now lives in
// MainLayout.razor, not in the challenge page's own markup.
window.k4l_playAudioFile = function (id, src) {
    var el = document.getElementById(id);
    if (!el) return;
    el.src = src;
    el.play();
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