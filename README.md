This is a basic demonstration Windows Phone 8.1 app that contains a reusable `GifImage` control
to display animated GIF images. It is not complete, and any serious use of it will require
many improvements to make it usable. It lacks lots of checking, and so it will probably crash
under certain conditions.

What needs fixing to make it actually usable:

* Allow setting the `Source` before the template is applied, then once the template is applied,
  start the animation. This will make setting the `Source` in XAML work.
* Add some dependency properties, such as `IsPlaying` and `FrameNumber`.
* Enable loading of image sources similar to how the `Image` control does it. That is, it
  should be able to load a GIF from the network, not just from the application resources.
