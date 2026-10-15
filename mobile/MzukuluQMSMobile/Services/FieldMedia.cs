namespace MzukuluQMSMobile.Services;
public interface IFieldMedia {Task<FileResult?> PickAsync(bool camera);}
public sealed class FieldMedia : IFieldMedia
{
 public async Task<FileResult?> PickAsync(bool camera)
 {
  if(camera && !MediaPicker.Default.IsCaptureSupported)throw new InvalidOperationException("No camera is available. Choose a gallery image.");
  var options=new MediaPickerOptions{Title="Inspection photo",SelectionLimit=1,MaximumWidth=2000,MaximumHeight=2000,CompressionQuality=90,RotateImage=true};
  try{return camera?await MediaPicker.Default.CapturePhotoAsync(options):(await MediaPicker.Default.PickPhotosAsync(options)).FirstOrDefault();}
  catch(PermissionException){throw new InvalidOperationException("Camera or photo permission was denied. Enable permission in device settings or choose another capture method.");}
 }
}
