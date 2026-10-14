# Why this DLL is here

`com.google.xr.extensions` (Android XR Extensions) uses `System.Runtime.CompilerServices.Unsafe.As<,>()` in
`Runtime/Internal/XRFutureHandler.cs`. Unity Collections used to bundle this assembly; Collections 2.6.8+
no longer does, and glTFast 6.20 requires Collections >= 2.6.8. Google's maintainers recommend adding the
assembly manually (https://github.com/android/android-xr-unity-package/issues/25).

This copy comes from the Unity installation
(`Editor/Data/NetStandard/EditorExtensions/System.Runtime.CompilerServices.Unsafe.dll`).
Remove this folder once a fixed Android XR Extensions release no longer needs it.
