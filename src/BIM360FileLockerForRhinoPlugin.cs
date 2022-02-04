using System;
using System.IO;
using System.Threading.Tasks;

using Rhino;
using Rhino.UI;
using Rhino.PlugIns;

using Grasshopper;
using Grasshopper.Kernel;

using EasyADC;

namespace BIM360FileLockerForRhino
{
    public class BIM360FileLockerForRhinoPlugin : PlugIn
    {
        public BIM360FileLockerForRhinoPlugin() => Instance = this;
        public static BIM360FileLockerForRhinoPlugin Instance { get; private set; }
        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        public static string PluginName { get; } = "BIM360 File Locker";

        public static bool SetReadOnly { get; private set; } = false;

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
            RhinoDoc.BeginOpenDocument += RhinoDoc_OpenDocument;
            RhinoDoc.CloseDocument += RhinoDoc_CloseDocument;
        }

        void SubscribeToGrasshopperChanges()
        {
            Instances.DocumentServer.DocumentAdded += GrasshopperDocumentServer_DocumentAdded;
            Instances.DocumentServer.DocumentRemoved += GrasshopperDocumentServer_DocumentRemoved;
        }

        async void RhinoDoc_OpenDocument(object sender, DocumentOpenEventArgs e)
        {
            if (e.FileName is string filePath)
            {
                // if file is imported, skip
                if (e.Merge)
                    Log($"Skip: Imported file {filePath}");

                // if file is none 3dm, it is going to get merged into a new 3dm, so skip
                else if (!(Path.GetExtension(filePath).ToLower() == ".3dm"))
                    Log($"Skip: Merged file {filePath}");

                // otherwise, checkin the file
                else
                    await CheckInFile(filePath);
            }
        }

        async void RhinoDoc_CloseDocument(object sender, DocumentEventArgs e)
        {
            if (e.Document.Path is string filePath)
                await CheckOutFile(filePath);
        }

        async void GrasshopperDocumentServer_DocumentAdded(object sender, object doc)
        {
            if (doc is GH_Document ghDoc && ghDoc.IsFilePathDefined)
                await CheckInFile(ghDoc.FilePath);
        }

        async void GrasshopperDocumentServer_DocumentRemoved(object sender, object doc)
        {
            if (doc is GH_Document ghDoc && ghDoc.IsFilePathDefined)
                await CheckOutFile(ghDoc.FilePath);
        }

        Task RunAndCaptureExceptions(Func<Task> action)
        {
            try { return action(); }
            catch (Exception ex)
            {
                Log(ex);
                return Task.CompletedTask;
            }
        }

        Task CheckInFile(string filePath)
        {
            return RunAndCaptureExceptions(() =>
            {
                // try to find the file in adc drives
                if (_adc.Contains(filePath))
                {
                    if (_adc.IsLockedByOther(filePath))
                    {
                        ReadOnlyFile(filePath);
                        NotifyLockedByOther(filePath);
                    }
                    else
                    {
                        return Task.Run(() =>
                        {
                            LockFile(filePath, setReadOnly: true);
                            NotifyLocked(filePath);
                        });
                    }
                }
                else
                    Log($"Skip: File is not on ADC drive {filePath}");

                return Task.CompletedTask;
            });
        }

        Task CheckOutFile(string filePath)
        {
            // try to find the file in adc drives
            if (_adc.Contains(filePath))
            {
                if (!_adc.IsLockedByOther(filePath))
                {
                    return Task.Run(() =>
                    {
                        UnlockFile(filePath);
                        SyncFile(filePath);
                        NotifyUnLocked(filePath);
                    });
                }
                else
                    UnReadOnlyFile(filePath);
            }
            else
                Log($"File is not on ADC drive {filePath}");

            return Task.CompletedTask;
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

        void ReadOnlyFile(string filePath)
        {
            if (SetReadOnly)
            {
                Log($"Set ReadOnly {filePath}");
                File.SetAttributes(filePath, FileAttributes.ReadOnly);
            }
        }

        void UnReadOnlyFile(string filePath)
        {
            if (SetReadOnly)
            {
                Log($"Clear ReadOnly {filePath}");
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }

        void LockFile(string filePath, bool setReadOnly = false)
        {
            Log($"Locking {filePath}");
            if (!_adc.LockFile(filePath))
                Log($"Failed locking {filePath}");
            else if (setReadOnly)
            {
            }
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