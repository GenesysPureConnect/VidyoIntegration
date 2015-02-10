using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Windows;
using AutoCompleteTextBoxLib;

namespace ININ.Alliances.AutoCompleteTextBoxTestHost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _names = new List<string>
        {
            "Ora Otte",
            "Vonda Roller",
            "Ashley Ogden",
            "Nydia Garibaldi",
            "Lera Grayer",
            "Wilfredo Guidroz",
            "Charissa Jamison",
            "Elisha Vanzile",
            "Lavon Hopkin",
            "Madlyn Spurgeon",
            "Antione Weakley",
            "Jacques Garg",
            "Aida Andre",
            "Jenni Bergeson",
            "Patty Kennell",
            "Kristian Audia",
            "Marcella Falgoust",
            "Eugenie Laux",
            "Kimbra Hart",
            "Coralie Frost",
            "Marry Cajigas",
            "Devon Case",
            "Odilia Shemwell",
            "Erika Jaworski",
            "Adrianna Huggard",
            "Catheryn Ruston",
            "Adrianne Latimer",
            "Suanne Cosenza",
            "Lyndsay Ruocco",
            "Madison Auer",
            "Una Rosse",
            "Gabriel Fenn",
            "Vivienne Monterroso",
            "Lannie Mcphillips",
            "Linnea Smullen",
            "Rocio Granato",
            "Amira Keeble",
            "Lachelle Slama",
            "Vivian Selby",
            "Venessa Delozier",
            "Marquis Saulter",
            "Lilli Chesley",
            "Vinnie Beltran",
            "Fe Ring",
            "Martine Cutchin",
            "Marta Easterly",
            "Aracelis Fordyce",
            "Gilma Mikus",
            "Meryl Hunger",
            "Noelia Konkel",
            "Arielle Toland",
            "Chet Huie",
            "Kieth Lena",
            "Elba Rabideau",
            "Gustavo Comer",
            "Mason Wainright",
            "Anderson Mize",
            "Leighann Coache",
            "Breanna Ho",
            "Isela Emily",
            "Iraida Sarinana",
            "Jacqualine Leaf",
            "Viviana Desoto",
            "Kary Sudderth",
            "Ty Duley",
            "Phebe Manjarrez",
            "Beverley Yocom",
            "Grover Marotz",
            "Whitley Beyers",
            "Kristen Commander",
            "Charis Poland",
            "Tisha Blomquist",
            "Lorina Laforest",
            "Ethelene Duford",
            "Jeanna Jahnke",
            "Cecelia Fretwell",
            "Lashay Figueras",
            "Ka Knudtson",
            "Roxy Szabo",
            "Antonia Ketchum",
            "Jill Castiglia",
            "Keri Mazzella",
            "Taren Diggs",
            "Vernell Bagley",
            "Javier Vanish",
            "Keira Cliett",
            "Annetta Barrentine",
            "Augustine Kendig",
            "Madeline Gillingham",
            "Ok Sunde",
            "Nga Belser",
            "Trula Sennett",
            "Colton Khalsa",
            "Sheena Dodson",
            "Jacquelyn Pineo",
            "Maryland Banegas",
            "Danial Albright",
            "Denna Falco",
            "Era Bristol",
            "Bessie Isherwood"
        };

        public bool UseDeferredFiltering { get; set; }

        public IEnumerable<object> Names { get { return _names; } }

        public MainWindow()
        {
            InitializeComponent();
            Box.Comparer = new StartsWithComparer();
        }

        private void BoxOnFilterTextChanged(FilterTextChangedEventArgs e)
        {
            try
            {
                // Do we want to handle this request?
                if (!UseDeferredFiltering) return;

                /* Kick off the process on a new thread. This isn't strictly necessary since the text box's eventing is 
                 * multithreaded, but simulates an asynchronous filter process.
                 * 
                 * In production situations, it may be prudent to cancel any pending filter operations before kicking off a 
                 * new one. The control will handle outdated responses (via GUID matching) without issue, but canceling any 
                 * long running filter processes might be prudent here.
                 */
                var t = new Thread(Filter);
                t.Start(e);

                // Mark it as handled
                e.DeferFiltering = true;
                e.Handled = true;

                // If we need to, adjust the timeout duration
                //e.FilterWaitTimeoutMs = 10000;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void Filter(object data)
        {
            try
            {
                // Get the args
                var args = (FilterTextChangedEventArgs) data;
                Console.WriteLine(DateTime.Now.ToLongTimeString() + " - Deferred text: " + args.Text + " guid:"+args.EventId);

                // Sleep to simulate a lookup
                Thread.Sleep(2000);

                // Complete the filtering
                var names = _names.Where(name => name.ToLower().Contains(args.Text.ToLower())).OrderBy(name => name);
                args.Source.CompleteDeferredFilter(names, args.EventId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }

    internal class StartsWithComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            try
            {
                // Filter text
                var xx = x == null ? "" : x.ToString().ToLower();

                // Item to compare
                var yy = y == null ? "" : y.ToString().ToLower();

                if (string.IsNullOrEmpty(xx) || string.IsNullOrEmpty(yy)) return -1;

                return yy.StartsWith(xx) ? 1 : -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }
    }
}
