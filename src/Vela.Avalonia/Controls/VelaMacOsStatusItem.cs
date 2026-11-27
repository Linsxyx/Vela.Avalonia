using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Native macOS AppKit <c>NSStatusItem</c> bridge used by <see cref="VelaTrayIcon"/> for interactive modes
/// (<see cref="VelaTrayPresentation.VelaMenu"/>, <see cref="VelaTrayPresentation.VelaPopover"/>, and
/// <see cref="VelaTrayPresentation.CommandOnly"/>).
/// Avalonia's built-in macOS <c>TrayIcon.Clicked</c> event is not raised on macOS; this lightweight,
/// Native-AOT-compatible Objective-C runtime bridge wires <c>NSStatusBarButton</c> target/action directly
/// so clicks respond immediately and can activate the application before showing a floating Vela flyout.
/// </summary>
internal sealed class VelaMacOsStatusItem : IDisposable
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const double SquareStatusItemLength = -2.0; // NSSquareStatusItemLength
    private const ulong EventMaskLeftMouseDown = 1UL << 1;
    private const ulong EventMaskRightMouseDown = 1UL << 3;

    private static readonly object TargetClassLock = new();
    private static nint _targetClass;
    private static nint _clickedSelector;
    private static ActionCallbackDelegate? _pinnedCallback;
    private static readonly ConcurrentDictionary<nint, WeakReference<VelaMacOsStatusItem>> Targets = new();

    private nint _statusItem;
    private nint _button;
    private nint _target;
    private bool _isDisposed;

    public event EventHandler? Clicked;

    public bool IsCreated => _statusItem != nint.Zero && !_isDisposed;

    public VelaMacOsStatusItem()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            EnsureTargetClass();

            var statusBarClass = objc_getClass("NSStatusBar");
            var systemStatusBar = objc_msgSend_RetPtr(statusBarClass, sel_registerName("systemStatusBar"));
            if (systemStatusBar == nint.Zero)
                return;

            var item = objc_msgSend_RetPtr_Double(
                systemStatusBar,
                sel_registerName("statusItemWithLength:"),
                SquareStatusItemLength);
            if (item == nint.Zero)
                return;

            // Retain the status item so it stays alive until Dispose()
            objc_msgSend_RetPtr(item, sel_registerName("retain"));

            var button = objc_msgSend_RetPtr(item, sel_registerName("button"));
            if (button == nint.Zero)
            {
                objc_msgSend_Void_Ptr(systemStatusBar, sel_registerName("removeStatusItem:"), item);
                objc_msgSend_Void(item, sel_registerName("release"));
                return;
            }

            var targetAlloc = objc_msgSend_RetPtr(_targetClass, sel_registerName("alloc"));
            var target = objc_msgSend_RetPtr(targetAlloc, sel_registerName("init"));
            if (target == nint.Zero)
            {
                objc_msgSend_Void_Ptr(systemStatusBar, sel_registerName("removeStatusItem:"), item);
                objc_msgSend_Void(item, sel_registerName("release"));
                return;
            }

            _statusItem = item;
            _button = button;
            _target = target;
            Targets[target] = new WeakReference<VelaMacOsStatusItem>(this);

            objc_msgSend_Void_Ptr(button, sel_registerName("setTarget:"), target);
            objc_msgSend_Void_Ptr(button, sel_registerName("setAction:"), _clickedSelector);
            objc_msgSend_RetPtr_ULong(
                button,
                sel_registerName("sendActionOn:"),
                EventMaskLeftMouseDown | EventMaskRightMouseDown);
        }
        catch
        {
            Dispose();
        }
    }

    public void SetIconPng(byte[] pngBytes, bool isTemplate, double pointSize = 18.0)
    {
        if (!IsCreated || _button == nint.Zero || pngBytes.Length == 0)
            return;

        try
        {
            var handle = GCHandle.Alloc(pngBytes, GCHandleType.Pinned);
            try
            {
                var nsDataClass = objc_getClass("NSData");
                var data = objc_msgSend_RetPtr_Ptr_NUInt(
                    nsDataClass,
                    sel_registerName("dataWithBytes:length:"),
                    handle.AddrOfPinnedObject(),
                    (nuint)pngBytes.Length);
                if (data == nint.Zero)
                    return;

                var nsImageClass = objc_getClass("NSImage");
                var allocImg = objc_msgSend_RetPtr(nsImageClass, sel_registerName("alloc"));
                var image = objc_msgSend_RetPtr_Ptr(allocImg, sel_registerName("initWithData:"), data);
                if (image == nint.Zero)
                    return;

                try
                {
                    objc_msgSend_Void_CGSize(image, sel_registerName("setSize:"), new CGSize(pointSize, pointSize));
                    objc_msgSend_Void_Byte(image, sel_registerName("setTemplate:"), isTemplate ? (byte)1 : (byte)0);
                    objc_msgSend_Void_Ptr(_button, sel_registerName("setImage:"), image);
                }
                finally
                {
                    objc_msgSend_Void(image, sel_registerName("release"));
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            // Ignore malformed image updates safely.
        }
    }

    public void SetToolTip(string? toolTipText)
    {
        if (!IsCreated || _button == nint.Zero)
            return;

        try
        {
            if (string.IsNullOrEmpty(toolTipText))
            {
                objc_msgSend_Void_Ptr(_button, sel_registerName("setToolTip:"), nint.Zero);
                return;
            }

            var utf8 = Marshal.StringToCoTaskMemUTF8(toolTipText);
            try
            {
                var nsStringClass = objc_getClass("NSString");
                var nsStr = objc_msgSend_RetPtr_Ptr(nsStringClass, sel_registerName("stringWithUTF8String:"), utf8);
                objc_msgSend_Void_Ptr(_button, sel_registerName("setToolTip:"), nsStr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }
        catch
        {
            // Ignore tooltip update failures safely.
        }
    }

    public void SetIsVisible(bool isVisible)
    {
        if (!IsCreated)
            return;

        try
        {
            objc_msgSend_Void_Byte(_statusItem, sel_registerName("setVisible:"), isVisible ? (byte)1 : (byte)0);
        }
        catch
        {
            // Ignore visibility update failures safely.
        }
    }

    /// <summary>
    /// Activates the current macOS application so a floating tray flyout appears in front of other apps
    /// and receives focus and light-dismiss deactivation events reliably.
    /// </summary>
    public static void ActivateApplication()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            var nsApp = objc_msgSend_RetPtr(nsAppClass, sel_registerName("sharedApplication"));
            if (nsApp != nint.Zero)
                objc_msgSend_Void_Byte(nsApp, sel_registerName("activateIgnoringOtherApps:"), 1);
        }
        catch
        {
            // Safe no-op if activation fails.
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_button != nint.Zero)
        {
            try
            {
                objc_msgSend_Void_Ptr(_button, sel_registerName("setTarget:"), nint.Zero);
                objc_msgSend_Void_Ptr(_button, sel_registerName("setAction:"), nint.Zero);
            }
            catch
            {
                // Ignore teardown failures.
            }
            _button = nint.Zero;
        }

        if (_statusItem != nint.Zero)
        {
            try
            {
                var statusBarClass = objc_getClass("NSStatusBar");
                var systemStatusBar = objc_msgSend_RetPtr(statusBarClass, sel_registerName("systemStatusBar"));
                if (systemStatusBar != nint.Zero)
                    objc_msgSend_Void_Ptr(systemStatusBar, sel_registerName("removeStatusItem:"), _statusItem);
                objc_msgSend_Void(_statusItem, sel_registerName("release"));
            }
            catch
            {
                // Ignore teardown failures.
            }
            _statusItem = nint.Zero;
        }

        if (_target != nint.Zero)
        {
            Targets.TryRemove(_target, out _);
            try
            {
                objc_msgSend_Void(_target, sel_registerName("release"));
            }
            catch
            {
                // Ignore teardown failures.
            }
            _target = nint.Zero;
        }
    }

    private static void EnsureTargetClass()
    {
        if (_targetClass != nint.Zero)
            return;

        lock (TargetClassLock)
        {
            if (_targetClass != nint.Zero)
                return;

            _clickedSelector = sel_registerName("onVelaTrayClicked:");

            var existing = objc_getClass("VelaTrayStatusTarget");
            if (existing != nint.Zero)
            {
                _targetClass = existing;
                return;
            }

            var nsObject = objc_getClass("NSObject");
            var cls = objc_allocateClassPair(nsObject, "VelaTrayStatusTarget", 0);
            if (cls == nint.Zero)
                return;

            _pinnedCallback = OnNativeStatusItemClicked;
            var fnPtr = Marshal.GetFunctionPointerForDelegate(_pinnedCallback);
            class_addMethod(cls, _clickedSelector, fnPtr, "v@:@");
            objc_registerClassPair(cls);
            _targetClass = cls;
        }
    }

    private static void OnNativeStatusItemClicked(nint self, nint cmd, nint sender)
    {
        if (!Targets.TryGetValue(self, out var weak) || !weak.TryGetTarget(out var owner) || owner._isDisposed)
            return;

        if (Dispatcher.UIThread.CheckAccess())
            owner.RaiseClicked();
        else
            Dispatcher.UIThread.Post(owner.RaiseClicked);
    }

    private void RaiseClicked()
    {
        if (!_isDisposed)
            Clicked?.Invoke(this, EventArgs.Empty);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActionCallbackDelegate(nint self, nint cmd, nint sender);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize(double width, double height)
    {
        public readonly double Width = width;
        public readonly double Height = height;
    }

    [DllImport(ObjCLib)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjCLib)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjCLib)]
    private static extern nint objc_allocateClassPair(
        nint superclass,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        nuint extraBytes);

    [DllImport(ObjCLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool class_addMethod(
        nint cls,
        nint name,
        nint imp,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(ObjCLib)]
    private static extern void objc_registerClassPair(nint cls);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_RetPtr(nint receiver, nint selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_RetPtr_Ptr(nint receiver, nint selector, nint arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_RetPtr_Double(nint receiver, nint selector, double arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_RetPtr_ULong(nint receiver, nint selector, ulong arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_RetPtr_Ptr_NUInt(nint receiver, nint selector, nint arg1, nuint arg2);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void(nint receiver, nint selector);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Ptr(nint receiver, nint selector, nint arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Byte(nint receiver, nint selector, byte arg1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_CGSize(nint receiver, nint selector, CGSize arg1);
}
