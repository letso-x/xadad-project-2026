namespace MzukuluQMSMobile.Services;
public interface IFieldMedia {Task<FileResult?> PickAsync(bool camera);}
public sealed class FieldMedia : IFieldMedia
{
 public async Task<FileResult?> PickAsync(bool camera)
 {
  try
  {
#if WINDOWS
   // Select an existing image without requiring Windows camera/media-capture components.
   if(!camera)return await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="Inspection photo",FileTypes=FilePickerFileType.Images});
#endif
   if(camera && !MediaPicker.Default.IsCaptureSupported)throw new InvalidOperationException("No camera is available. Choose a gallery image.");
   var options=new MediaPickerOptions{Title="Inspection photo",SelectionLimit=1,MaximumWidth=2000,MaximumHeight=2000,CompressionQuality=90,RotateImage=true};
   return camera?await MediaPicker.Default.CapturePhotoAsync(options):(await MediaPicker.Default.PickPhotosAsync(options)).FirstOrDefault();
  }
  catch(PermissionException){throw new InvalidOperationException("Camera or photo permission was denied. Enable permission in device settings or choose another capture method.");}
#if WINDOWS
  catch(System.Runtime.InteropServices.COMException error)
  {
   WinUI.StartupTrace.Mark("Optional photo action failed HRESULT "+error.HResult.ToString("X8")+Environment.NewLine+error.StackTrace);
   throw new InvalidOperationException("Windows could not open the photo component. Your saved work is retained. Choose an existing image or try again after checking Windows camera/file access.",error);
  }
#endif
 }
}