# Third-party components in the LibVLC experiment

The Hazz licence does not replace the licences of these components. Copies of
their notices are included here. Libraries are unmodified, dynamically loaded
and distributed as separate replaceable DLLs. Modification, replacement,
relinking and reverse engineering for debugging modifications to the LGPL
components are permitted under their licences, notwithstanding restrictions on
Hazz branding. Hazz source and project files accompany this release.

* LibVLCSharp and LibVLCSharp.WPF 3.10.1 — Copyright VideoLAN and contributors;
  LGPL 2.1 or later. [Source, including build instructions](https://github.com/videolan/libvlcsharp/tree/3.10.1).
  [Source archive](https://github.com/videolan/libvlcsharp/archive/refs/tags/3.10.1.zip).
* VideoLAN.LibVLC.Windows 3.0.23.1 — Copyright VideoLAN and contributors;
  package declared LGPL 2.1 or later. Includes LibVLC 3.0.23 and native modules.
  [VLC source and component notices](https://github.com/videolan/vlc/tree/3.0.23).
  [Release source archive](https://download.videolan.org/pub/videolan/vlc/3.0.23/vlc-3.0.23.tar.xz).
  [Native package/build scripts](https://code.videolan.org/videolan/libvlc-nuget).
  VLC's COPYING and COPYING.LIB describe component-specific terms; bundled
  dependencies retain their own licences. The VLC source's `contrib/src`
  directory identifies dependency versions, patches and source locations.

To build or substitute these libraries, use the upstream instructions and the
matching x64 architecture. Keep `LibVLCSharp.dll`, `LibVLCSharp.WPF.dll`, and
`libvlc/win-x64` accessible next to the Hazz executable. Do not remove the native
plugin directory. The project uses NuGet to restore original dependencies.

Existing .NET, NAudio, SQLite and GIF dependencies retain their existing terms.
The third-party code is not authored by Hazz and is not covered by Hazz's
restriction on rebranded redistributions.
