Gateway
=======

The software used in the Gateway Project for DDS.


Purpose
-------
Create a proof of concept for the DDS and PTI project.


My working environment
----------------------
* Windows 8 x64
* Visual Studio 2012
* Kinect SDK 1.5
* 1x Kinect for Windows Sensor
* 1x Kinect for Xbox Sensot


TODO
----
* [DONE] Stream rear kinect video
* [DONE] Detect skeleton close to front sensor
* [DONE] Change from Live stream to SlideShow and vice-versa
* [DONE] SlideShow gestures
* [DONE] Visible Hands
* Design Changes
* Window Size
* Picture Folder+Content
* (Ads at the bottom)

Known issues - to be fixed
--------------------------
* N/A


Known issues - low priority
---------------------------
* The code does not handle changes in Kinect Status


Nota Bene
---------
* The Kinect DevicesID are hardcoded in the main C# file. You might need to change them, according to your own devices.
** Front Kinect ID: USB\VID_045E&PID_02C2\5&464F35A&0&2
** Rear  Kinect ID: USB\VID_0409&PID_005A\5&464F35A&0&1