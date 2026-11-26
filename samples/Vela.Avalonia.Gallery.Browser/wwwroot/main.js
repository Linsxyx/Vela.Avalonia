import { dotnet } from './_framework/dotnet.js';

const isBrowser = typeof window !== 'undefined';
if (!isBrowser) throw new Error('Expected to be running in a browser');

const outEl = document.getElementById('out');
const splashEl = document.getElementById('vela-splash');

function showSplashError(err) {
    console.error('[Vela.Avalonia] Failed to start WebAssembly Gallery:', err);
    if (!splashEl) return;
    const subEl = splashEl.querySelector('.splash-sub');
    const barEl = splashEl.querySelector('.splash-bar');
    if (barEl) barEl.remove();
    if (subEl) {
        subEl.classList.add('is-error');
        subEl.textContent = `Startup failed: ${err?.message || String(err)}`;
    }
}

try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();

    // Hide the splash overlay as soon as Avalonia attaches its canvas into #out
    if (outEl && splashEl) {
        const observer = new MutationObserver(() => {
            if (outEl.querySelector('canvas')) {
                splashEl.classList.add('is-hidden');
                setTimeout(() => splashEl.remove(), 260);
                observer.disconnect();
            }
        });
        observer.observe(outEl, { childList: true, subtree: true });
    }

    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (err) {
    showSplashError(err);
}
