using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageTemplate
{
    public partial class MainForm : Form
    {
        public RGBPixel[,] ImageMatrix;
        Segmenter segmenter = new Segmenter();
        string OpenedFilePath = "";
        Bitmap segmentedImage,colors;
        List<Color> selectedColors = new List<Color>();
        public MainForm()
        {
            InitializeComponent();

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            pictureBox2.MouseClick += pictureBoxSegmented_MouseClick;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (selectedColors.Count < 2)
                {
                    MessageBox.Show("Please select at least 2 regions to merge.");
                    return;
                }

                Color mergeColor = selectedColors[0];

                for (int y = 0; y < segmentedImage.Height; y++)
                {
                    for (int x = 0; x < segmentedImage.Width; x++)
                    {

                        Color pxColor = segmentedImage.GetPixel(x, y);
                        if (selectedColors.Contains(pxColor))
                        {
                            segmentedImage.SetPixel(x, y, mergeColor);
                        }
                    }
                }

                selectedColors.Clear();
                colors = new Bitmap(segmentedImage.Width, segmentedImage.Height);
                byte r = mergeColor.R;
                byte g = mergeColor.G;
                byte b = mergeColor.B;
                for (int y = 0; y < segmentedImage.Height; y++)
                {
                    for (int x = 0; x < segmentedImage.Width; x++)
                    {
                    
                        if (segmentedImage.GetPixel(x,y).R == r && segmentedImage.GetPixel(x,y).G == g && segmentedImage.GetPixel(x,y).B == b)
                        {
                            byte rr = segmenter.ImageMatrix[y, x].red;
                            byte gg = segmenter.ImageMatrix[y, x].green;
                            byte bb = segmenter.ImageMatrix[y, x].blue;

                            colors.SetPixel(x, y, Color.FromArgb(rr, gg, bb));
                        }
                        else
                        {
                            colors.SetPixel(x, y, Color.FromArgb(255, 255, 255));

                        }
                    }
                }
            }
            pictureBox2.Image = colors;

        }

        private Point GetImageCoordinates(PictureBox pb, MouseEventArgs e)
        {
            if (pb.Image == null) return Point.Empty;

            int imgWidth = pb.Image.Width;
            int imgHeight = pb.Image.Height;
            int boxWidth = pb.Width;
            int boxHeight = pb.Height;

            float ratioWidth = (float)boxWidth / imgWidth;
            float ratioHeight = (float)boxHeight / imgHeight;
            float ratio = Math.Min(ratioWidth, ratioHeight);

            int displayedWidth = (int)(imgWidth * ratio);
            int displayedHeight = (int)(imgHeight * ratio);
            int offsetX = (boxWidth - displayedWidth) / 2;
            int offsetY = (boxHeight - displayedHeight) / 2;

            if (e.X < offsetX || e.X >= offsetX + displayedWidth ||
                e.Y < offsetY || e.Y >= offsetY + displayedHeight)
                return Point.Empty;

            int x = (int)((e.X - offsetX) / ratio);
            int y = (int)((e.Y - offsetY) / ratio);

            return new Point(x, y);
        }

        private void pictureBoxSegmented_MouseClick(object sender, MouseEventArgs e)
        {
            if (segmentedImage == null)
            {
                MessageBox.Show("No segmented image available.");
                return;
            }

            Point imgPoint = GetImageCoordinates(pictureBox2, e);
            if (imgPoint == Point.Empty)
            {
                MessageBox.Show("Clicked outside the image.");
                return;
            }

            Color clickedColor = segmentedImage.GetPixel(imgPoint.X, imgPoint.Y);

            if (!selectedColors.Contains(clickedColor))
            {
                selectedColors.Add(clickedColor);            }
        }


        public static string ShowInputDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };

            Label textLabel = new Label() { Left = 10, Top = 10, Text = text, AutoSize = true };
            TextBox inputBox = new TextBox() { Left = 10, Top = 35, Width = 260 };
            Button confirmation = new Button() { Text = "OK", Left = 200, Width = 70, Top = 65, DialogResult = DialogResult.OK };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : null;
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                OpenedFilePath = openFileDialog1.FileName; // Save it globally
                ImageMatrix = ImageOperations.OpenImage(OpenedFilePath);
                ImageOperations.DisplayImage(ImageMatrix, pictureBox1);

                txtWidth.Text = ImageOperations.GetWidth(ImageMatrix).ToString();
                txtHeight.Text = ImageOperations.GetHeight(ImageMatrix).ToString();
            }
        }

        private async void BtnGaussSmooth_Click(object sender, EventArgs e)
        {
            try
            {
                if (ImageMatrix == null)
                {
                    MessageBox.Show("Please open an image first.");
                    return;
                }

                if (!double.TryParse(txtGaussSigma.Text, out double sigma))
                {
                    MessageBox.Show("Please enter a valid sigma value (e.g., 0.8)");
                    return;
                }

                string kInput = ShowInputDialog("Enter the value of k:", "Segmentation Parameter");
                if (string.IsNullOrWhiteSpace(kInput) || !int.TryParse(kInput, out int k) || k <= 0)
                {
                    MessageBox.Show("Please enter a valid positive integer for k.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int maskSize = (int)nudMaskSize.Value;
                string imageFolder = Path.GetDirectoryName(OpenedFilePath); // Get folder for saving output

                btnGaussSmooth.Enabled = false;
                btnOpen.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var result = await Task.Run(() =>
                {
                    var filtered = ImageOperations.GaussianFilter1D(ImageMatrix, maskSize, sigma);
                    segmenter.preProcessing(filtered);

                    Stopwatch timer = Stopwatch.StartNew();
                    RGBPixel[,] segmented = segmenter.segmentImage(k, imageFolder);
                    timer.Stop();

                    return (segmented, time: timer.ElapsedMilliseconds);
                });

                ImageOperations.DisplayImage(result.segmented, pictureBox2);
                segmentedImage = new Bitmap(pictureBox2.Image);

                MessageBox.Show($"Segmentation took {result.time} milliseconds.\n\nSegment info saved to:\n{Path.Combine(Path.GetDirectoryName(OpenedFilePath), "segments.txt")}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation failed: {ex.Message}\n\nDetails:\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGaussSmooth.Enabled = true;
                btnOpen.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Optional
        }
    }
}

