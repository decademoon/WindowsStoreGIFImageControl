using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Gif
{
    public sealed class GifImage : Control
    {
        readonly IEnumerable<string> EMPTY_ENUMERABLE_STRING = Enumerable.Empty<string>();

        Image displayFrame;
        Storyboard storyboard;
        ObjectAnimationUsingKeyFrames animation;

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(GifImage), new PropertyMetadata(null, onSourceChanged));

        static async void onSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gifImage = (GifImage)d;
            await gifImage.decodeGif((Uri)e.NewValue);
        }

        public GifImage()
        {
            this.DefaultStyleKey = typeof(GifImage);
            storyboard = new Storyboard();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            displayFrame = this.GetTemplateChild("DisplayFrame") as Image;

            // prepare new storyboard animation
            animation = new ObjectAnimationUsingKeyFrames();
            Storyboard.SetTarget(animation, displayFrame);
            Storyboard.SetTargetProperty(animation, "Source");
            storyboard.Children.Add(animation);
        }

        async Task decodeGif(Uri source)
        {
            storyboard.Stop();
            animation.KeyFrames.Clear();

            TimeSpan cumulativeDelay;

            var file = await StorageFile.GetFileFromApplicationUriAsync(source);
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
                byte[] imageBuffer = new byte[decoder.PixelWidth * decoder.PixelHeight * 4];

                // fill with opaque black
                for (int i = 0; i < imageBuffer.Length; i += 4)
                {
                    imageBuffer[i + 0] = 0;
                    imageBuffer[i + 1] = 0;
                    imageBuffer[i + 2] = 0;
                    imageBuffer[i + 3] = 255;
                }

                for (uint i = 0; i < decoder.FrameCount; i++)
                {
                    // get pixel data for this frame
                    var frame = await decoder.GetFrameAsync(i);
                    var data = await frame.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                    var pixels = data.DetachPixelData();

                    // get frame properties
                    var properties = await frame.BitmapProperties.GetPropertiesAsync(EMPTY_ENUMERABLE_STRING);
                    var imgdesc = await ((BitmapPropertiesView)properties["/imgdesc"].Value).GetPropertiesAsync(EMPTY_ENUMERABLE_STRING);
                    var grctlext = await ((BitmapPropertiesView)properties["/grctlext"].Value).GetPropertiesAsync(EMPTY_ENUMERABLE_STRING);

                    var delay = (ushort)grctlext["/Delay"].Value;
                    var transparencyFlag = (bool)grctlext["/TransparencyFlag"].Value;
                    var disposal = (byte)grctlext["/Disposal"].Value;
                    var t = (ushort)imgdesc["/Top"].Value;
                    var l = (ushort)imgdesc["/Left"].Value;
                    var w = (ushort)imgdesc["/Width"].Value;
                    var h = (ushort)imgdesc["/Height"].Value;

                    // disposal values:
                    // 0: no disposal specified
                    // 1: do not dispose; graphic to be left in place
                    // 2: restore background color
                    // 3: restore to previous

                    if (disposal == 2)
                    {
                        // fill with background color
                        // TODO: figure out what the actual background color should be, but in the meantime
                        // we'll assume it's transparent
                        Array.Clear(imageBuffer, 0, imageBuffer.Length);
                    }

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            var sourceOffset = (y * w + x) * 4;
                            var destOffset = ((t + y) * decoder.PixelWidth + l + x) * 4;

                            if (pixels[sourceOffset + 3] == 255)
                            {
                                imageBuffer[destOffset + 0] = pixels[sourceOffset + 0];
                                imageBuffer[destOffset + 1] = pixels[sourceOffset + 1];
                                imageBuffer[destOffset + 2] = pixels[sourceOffset + 2];
                                imageBuffer[destOffset + 3] = 255;
                            }
                        }
                    }

                    // convert frame to WritableBitmap
                    var bmp = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
                    using (var bmpStream = bmp.PixelBuffer.AsStream())
                    {
                        bmpStream.Seek(0, SeekOrigin.Begin);
                        await bmpStream.WriteAsync(imageBuffer, 0, imageBuffer.Length);
                    }

                    // add animation frame
                    var keyFrame = new DiscreteObjectKeyFrame();
                    keyFrame.KeyTime = KeyTime.FromTimeSpan(cumulativeDelay);
                    keyFrame.Value = bmp;
                    animation.KeyFrames.Add(keyFrame);

                    // add frame delay
                    cumulativeDelay = cumulativeDelay.Add(TimeSpan.FromMilliseconds(delay * 10));
                }

                storyboard.RepeatBehavior = decoder.FrameCount == 1 ? new RepeatBehavior(1) : RepeatBehavior.Forever;
            }

            storyboard.Begin();
        }
    }
}
