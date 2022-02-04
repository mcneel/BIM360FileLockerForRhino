using System;
using System.IO;

using Rhino;
using Rhino.UI;
using Rhino.PlugIns;

using Grasshopper;
using Grasshopper.Kernel;

using EasyADC;

namespace BIM360FileLockerForRhino
{
    public class BIM360FileLockerForRhinoPlugin : Rhino.PlugIns.PlugIn
    {
        public BIM360FileLockerForRhinoPlugin() => Instance = this;
        public static BIM360FileLockerForRhinoPlugin Instance { get; private set; }
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        public static string PluginName { get; } = "BIM360 File Locker";

        static void Log(string message)
        {
#if DEBUG
            RhinoApp.WriteLine($"[{PluginName}] {message}");
#endif
        }

        static void Log(Exception exception) => Log(exception.ToString());

        ADC _adc = default;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // init ADC api and capture exceptions
            try
            {
                // connect to api and hold for this session
                _adc = new ADC();

                // start listening to rhino doc events
                // we are gonna handle file locking using these events
                SubscribeToRhinoChanges();

                // start listening to grasshopper doc events
                // we are gonna handle file locking using these events
                SubscribeToGrasshopperChanges();

                Log("Successfully connected to ADC");

                return LoadReturnCode.Success;
            }
            catch (Exception e)
            {
                RhinoApp.WriteLine($"Error loading File locker {e}");
                errorMessage = e.ToString();
                return LoadReturnCode.ErrorNoDialog;
            }
        }

        void SubscribeToRhinoChanges()
        {
            RhinoDoc.EndOpenDocument += RhinoDoc_EndOpenDocument;
            RhinoDoc.CloseDocument += RhinoDoc_CloseDocument;
        }

        void SubscribeToGrasshopperChanges()
        {
            Instances.DocumentServer.DocumentAdded += GrasshopperDocumentServer_DocumentAdded;
            Instances.DocumentServer.DocumentRemoved += GrasshopperDocumentServer_DocumentRemoved;
        }

        void RhinoDoc_EndOpenDocument(object sender, DocumentOpenEventArgs e)
        {
            RunAndCaptureExceptions(() =>
            {
                if (e.FileName is string filePath)
                    CheckInFile(filePath);
            });
        }

        void RhinoDoc_CloseDocument(object sender, DocumentEventArgs e)
        {
            RunAndCaptureExceptions(() =>
            {
                if (e.Document.Path is string filePath)
                    CheckOutFile(filePath);
            });
        }

        void GrasshopperDocumentServer_DocumentAdded(object sender, object doc)
        {
            RunAndCaptureExceptions(() =>
            {
                if (doc is GH_Document ghDoc && ghDoc.IsFilePathDefined)
                    CheckInFile(ghDoc.FilePath);
            });
        }

        void GrasshopperDocumentServer_DocumentRemoved(object sender, object doc)
        {
            RunAndCaptureExceptions(() =>
            {
                if (doc is GH_Document ghDoc && ghDoc.IsFilePathDefined)
                    CheckOutFile(ghDoc.FilePath);
            });
        }

        void RunAndCaptureExceptions(Action action)
        {
            try { action(); }
            catch (Exception ex) { Log(ex); }
        }

        void CheckInFile(string filePath)
        {
            // try to find the file in adc drives
            if (_adc.Contains(filePath))
            {
                if (_adc.IsLockedByOther(filePath))
                    NotifyLockedByOther(filePath);
                else
                {
                    LockFile(filePath);
                    NotifyLocked(filePath);
                }
            }
            else
                Log($"File is not on ADC drive {filePath}");
        }

        void CheckOutFile(string filePath)
        {
            // try to find the file in adc drives
            if (_adc.Contains(filePath))
            {
                if (!_adc.IsLockedByOther(filePath))
                {
                    UnlockFile(filePath);
                    SyncFile(filePath);
                    NotifyUnLocked(filePath);
                }
            }
            else
                Log($"File is not on ADC drive {filePath}");
        }

        void NotifyLockedByOther(string filePath)
        {
            ADCFileInfo fileinfo = _adc.GetFileInfo(filePath);
            string fileName = Path.GetFileName(filePath);
            string owner = fileinfo.LockOwner;
            string lockTime = fileinfo.LockTimeStamp.ToString();

            Log($"File is already locked by {owner} @ {lockTime} {filePath}");
            Dialogs.ShowMessage(
                $"File is locked!\n\n" +
                $"\"{fileName}\" was locked by:\n\n" +
                $"Lock Owner:  {owner}\n" +
                $"Lock Time:   {lockTime}\n\n" +
                $"Any edits you make may be sent to recycle bin!",
                PluginName,
                ShowMessageButton.OK,
                ShowMessageIcon.Stop
            );
        }

        void NotifyLocked(string filePath)
        {
            RhinoApp.WriteLine($"{PluginName}: Locked \"{Path.GetFileName(filePath)}\"");
        }

        void NotifyUnLocked(string filePath)
        {
            RhinoApp.WriteLine($"{PluginName}: UnLocked \"{Path.GetFileName(filePath)}\"");
        }

        void LockFile(string filePath)
        {
            Log($"Locking {filePath}");
            if (!_adc.LockFile(filePath))
                Log($"Failed locking {filePath}");
        }

        void UnlockFile(string filePath)
        {
            Log($"Unlocking {filePath}");
            if (!_adc.UnlockFile(filePath))
                Log($"Failed unlocking {filePath}");
        }

        void SyncFile(string filePath)
        {
            Log($"Syncing {filePath}");
            _adc.SyncFile(filePath, forceSync: true);
        }
    }
}