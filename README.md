# BIM360 File Locker For Rhino

- This plugin alerts about the lock status, if Rhino or Grasshopper file is being opened from Autodesk Docs drive (Managed by Autodesk Desktop Connector - ADC)
- If the file being opened in not locked on BIM360, this plugin locks the file while it is open within Rhino or Grasshopper for the active user
- When closing the file, this plugin forces a sync on Autodesk Docs drive


<iframe width="560" height="315" src="https://www.youtube.com/embed/son3aC8kJ2c" title="YouTube video player" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>

## How To Install

Install from Package Manager in Rhino >= 7.13

## Developer Notes

- Currently this plugin is using beta release of [EasyADC](https://www.nuget.org/packages/EasyADC/) to communicate with Autodesk Desktop Connector