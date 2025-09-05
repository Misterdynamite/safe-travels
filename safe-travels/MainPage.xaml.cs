using safe_travels.API.AucklandTransportAPI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace safe_travels
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnSearch(object sender, EventArgs e)
        {
            Console.WriteLine("Button clicked");

            StopsCalls aTAPIDEMO = new StopsCalls();
            string searchText = BusSearch.Text;

            if (string.IsNullOrWhiteSpace(searchText))
                return;

            try
            {
                // Fetch data
                List<StopData> data = await aTAPIDEMO.testTask(searchText);

                // Bind the data to ListView
                BusListView.ItemsSource = data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data: {ex.Message}");
                await DisplayAlert("Error", "Could not fetch stops.", "OK");
            }
        }


    }
}