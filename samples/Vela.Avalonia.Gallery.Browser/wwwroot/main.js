import { dotnet } from './_framework/dotnet.js';

const isBrowser = typeof window !== 'undefined';
if (!isBrowser) throw new Error('Expected to be running in a browser');

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// Hide the splash overlay as soon as Avalonia attaches its canvas into #out
const outEl = document.getElementById('out');
const splashEl = document.getElementById('vela-splash');
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
