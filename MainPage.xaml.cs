using LocationGiver.Common;
using LocationGiver.Common.Enums;
using System.Text;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LocationGiver;
public partial class MainPage : ContentPage
{
    string m_filePath { get; set; }
    Byte[] m_imageContent { get; set; }
    string locationKeeperUrl = "https://localhost:5001/api/Location/";

    private Timer m_timer;
    private HttpClient m_client;
    private Person m_person;

    public MainPage()
	{
		InitializeComponent();
        m_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "data.json");
        ////File.Delete(m_filePath);
        GetPersonInfo();
        m_client = new HttpClient();
    }

    private void GetPersonInfo()
    {
        if (File.Exists(m_filePath))
        {
            var data = File.ReadAllText(m_filePath);
            m_person = JsonSerializer.Deserialize<Person>(data);
            if (m_person != null) // person exists
            {
                nameTextBox.Text = m_person.Name;
                numberTextBox.Text = m_person.MobileNumber;
                DisableEditMode();
                SetImage(m_person.ImageConent);
            }
            else
            {
                m_imageContent = null;
                EnableEditMode();
            }
        }
    }

    private void editImageButton_Clicked(object sender, EventArgs e)
    {
        EnableEditMode();
    }

    private void EnableEditMode()
    {
        SaveBtn.IsVisible = true;
        selectProfileButton.IsVisible = true;
        nameTextBox.IsReadOnly = false;
        numberTextBox.IsReadOnly = false;
        nameTextBox.IsReadOnly = false;
        numberTextBox.IsReadOnly = false;
        StartButton.IsVisible = false;
    }

    private void SaveButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var person = new Person()
            {
                Name = nameTextBox.Text,
                MobileNumber = numberTextBox.Text,
                ImageConent = m_imageContent,
            };
            File.WriteAllText(m_filePath, JsonSerializer.Serialize(person));
            GetPersonInfo();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

    }

    private void DisableEditMode()
    {
        SaveBtn.IsVisible = false;
        editImageButton.IsVisible = true;
        selectProfileButton.IsVisible = false;
        nameTextBox.IsReadOnly = true;
        numberTextBox.IsReadOnly = true;
        StartButton.IsVisible = true;
    }

    private async void OnSelectImageClicked(object sender, EventArgs e)
    {
        // Request permission to access the photo library
        var status = await Permissions.RequestAsync<Permissions.Media>();
        if (status != PermissionStatus.Granted)
        {
            // Permission denied, handle accordingly
            return;
        }

        // Launch the file picker to select an image
        var result = await FilePicker.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "Select an Image"
        });

        if (result != null)
        {
            // Get the selected image file's stream
            var stream = await result.OpenReadAsync();

            // Convert the stream to a byte array
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            // Perform the image upload using the imageBytes array
            await SetImage(imageBytes);
        }
    }



    private async Task SetImage(byte[] imageBytes)
    {
        m_imageContent = imageBytes;
        // Convert the byte array to ImageSource
        var stream = new MemoryStream(m_imageContent);
        var imageSource = ImageSource.FromStream(() => stream);
        profilePic.Source = imageSource;
    }

    private async Task SendLocationDataToApi(double latitude, double longitude)
    {
        try
        {
            var data = new LocationUpdateRequestApiModel()
            {
                Latitude = latitude,
                Longitude = longitude,
                MobileNumber = m_person.MobileNumber
            };
          
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await m_client.PostAsync(locationKeeperUrl+"location", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending location data to API: {ex.Message}");
        }
    }

    private async Task SendSignal(string code)
    {
        try
        {
            var data = new SignalRequestApiModel(){ Code = code, Name = m_person.Name, MobileNumber = m_person.MobileNumber };
            var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            var response = await m_client.PostAsync(locationKeeperUrl+"signal", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending location data to API: {ex.Message}");
        }
    }

    private void StartButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (StartButton.Text == "Start")
            {
                SendSignal("Start");
                StartButton.Text = "Stop";
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Code to run on the main thread
                    m_timer = new Timer(async _ => await RepeatMethod(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
                });

            }
            else
            {
                SendSignal("Stop");
                StartButton.Text = "Start";
                m_timer?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task RepeatMethod()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var location = await Geolocation.GetLocationAsync();
            if (location != null)
            {
                await SendLocationDataToApi(location.Latitude, location.Longitude);
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        m_timer?.Dispose();
    }
}

