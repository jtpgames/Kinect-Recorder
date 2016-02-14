# Important Note
This project was created as part of a module I took at the [University of Applied Sciences Muenster](https://www.fh-muenster.de/fb2/ueber_uns/index.php?p=0). Therefore the project should only be considered as a proof of concept.

# Kinect Recorder

The Kinect Recorder is an application utilizing the Kinect V2 and Microsofts Kinect 2.0 SDK to record video and audio. It currently has the following features:
 - Record raw streams and save them in *.xef files. Uses the Kinect Studio API. Be aware that at the time of this writing the Kinect Studio API does not allow a compressed color stream (who know's why ... the enum is there). This leads to HUGE files.
 - Open and playback recorded *.xef files. Uses the Kinect Studio API.
 - Filtering of undesired objects in front of the camera. The method is inspired by Oliver Lau and his project whiteboard minus one, see [1].
 - Recording the filtered video stream alongside the audio stream into a single *.mp4 file. (Experimental, see Todos)

### Version
0.42

### Tech

Kinect Recorder uses a number of open source projects to work properly:

* [SlimDX] - For GPU accelerated filtering.
* [SharpDX] - To access the media foundation api and implement video and audio recording.
* [MvvmLight] - To design the ui using the mvvm design pattern.
* [Reactive Extensions for .NET] - To handle events and data according to the Reactive Programming paradigm.

### Installation

In order to use the software you need to install the following software in the given order:
 - [Kinect SDK](https://www.microsoft.com/en-us/download/details.aspx?id=44561) - At least KinectSDK-v2.0_1409.
 - [Kinect Configuration Verifier Tool](https://dev.windows.com/en-us/kinect/tools) - To verify your system.
 - [SlimDX Runtime](https://slimdx.org/download.php) - I used SlimDX Runtime .NET 4.0 x64 (January 2012).

### Development

You're welcome to contribute or to make your own project.

Apart from the software mentioned above you are going to need the following:
 * [Visual Studio 2015 (Community Edition)](https://www.visualstudio.com/en-us/downloads/download-visual-studio-vs.aspx).
 * [Windows Software Development Kit (SDK) for Windows 10](https://dev.windows.com/en-us/downloads/windows-10-sdk).
    * Windows Headers, Libraries, and Metadata
 * [SlimDX Developer SDK](https://slimdx.org/download.php).

### Known issues / Todos

 * Recording.
    * Video- or audioframes are dropped by the Kinect runtime if the software does not run fast enough. This is a known problem, see [MSDN](https://social.msdn.microsoft.com/Forums/en-US/b5f44f48-12ea-42b9-b374-8b643609869f/reconstructing-wav-audio-from-specific-audio-beams?forum=kinectv2sdk).
    * Recorded audio has a great amount of noise. Multiple methods of storing the 32 Bit, 16 kH PCM Stream lead to the same result. It could be that some preprocessing (like echo cancellation) done by the Kinect runtime induces this noise.
 * Optimize GPU acceleration.
    * Use a deferred context so the method can execute on another thread without blocking the UI-Thread.
	* Optimize the copy operations using unsafe code.

License
----

MIT

References:
----

 - [1] [Oliver Lau, Tafel ohne Lehrer, Mit der Kinect 2 unerwünschte Objekte aus dem Videobild entfernen, c't 19/15, S. 156](http://www.heise.de/ct/ausgabe/2015-19-Mit-der-Kinect-2-unerwuenschte-Objekte-aus-dem-Videobild-entfernen-2779393.html?wt_mc=print.ct.2015.19.156#zsdb-article-links)

