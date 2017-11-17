using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace H5Bitmap
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		byte[] DDSHeader1 = new byte[] { 0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x0A, 0x00 };
		byte[] DDSHeader2 = new byte[] {0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x44, 0x58, 0x31,
			0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

		byte[] DDSHeader3 = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };

		public MainWindow()
		{
			InitializeComponent();

			mipbox.Items.Add(0);
			mipbox.Items.Add(1);
			mipbox.Items.Add(2);
			mipbox.SelectedIndex = 0;

			WriteLog("Drag .bitmap files anywhere above to convert. ONLY .BITMAP FILES!");
		}

		private void Window_Drop(object sender, DragEventArgs e)
		{
			if (checkpath.IsChecked == true && pathbox.Text == "")
			{
				WriteLog("set a path you dummy");
				return;
			}
			

			string[] draggedFiles = (string[])e.Data.GetData(DataFormats.FileDrop, true);

			List<int> badformats = new List<int>();

			foreach (string file in draggedFiles)
			{
				//check if this is a bitmap tag
				if (!file.EndsWith(".bitmap"))
				{
					WriteLog("this is not a .bitmap you dummy");
					continue;
				}

				//check how many raw images are in the same folder
				string path = file.Substring(0, file.LastIndexOf("\\") + 1);

				int slashloc = file.LastIndexOf("\\") + 1;

				string filename = file.Substring(slashloc, file.Length - slashloc - 7);

				string[] allfiles = Directory.GetFiles(path, filename + "*", SearchOption.TopDirectoryOnly);

				//go through files
				bool chunky = false;

				List<string> chunkfiles = new List<string>();
				List<string> bitmfiles = new List<string>();
				foreach (string pp in allfiles)
				{
					if (pp.Contains(".chunk"))
					{
						chunky = true;
						chunkfiles.Add(pp);
					}

					if (pp.EndsWith("_bitmap resource handle_]"))
						bitmfiles.Add(pp);
				}

				//read the bitmap tag file
				FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
				BinaryReader br = new BinaryReader(fs);

				List<bitminfo> infos = new List<bitminfo>();

				for (int i = bitmfiles.Count; i > 0; i--)
				{
					bitminfo temp = new bitminfo();

					int offset = 0x28 * i;

					fs.Position = fs.Length - offset;

					temp.Width = br.ReadInt16();
					temp.Height = br.ReadInt16();

					fs.Position += 4;

					temp.Format = br.ReadInt16();

					infos.Add(temp);
				}

				br.Close();
				fs.Close();
				//x160 for mip in normal resource

				byte[] bitm;

				List<string> ctxbitms = new List<string>();

				//grab and save each dds
				for (int i = 0; i < bitmfiles.Count; i++)
				{
					int newformat = GetFormat(infos[i].Format);

					bool ctxwarning = false;

					if (infos[i].Format == 40)
						ctxwarning = true;

					if (newformat == -1)
					{
						WriteLog("unsupported format " + infos[i].Format + " in file \"" + filename + " \". let me know about this somewhere");
						continue;
					}

					string imagepath = path + filename + ".bitmap[" + i + "_bitmap resource handle_]";


					if (chunky)
					{
						int chunkval = chunkfiles.Count - 1;
						if (mipbox.SelectedIndex < chunkval)
						{
							chunkval -= mipbox.SelectedIndex;
							infos[i].Width /= (mipbox.SelectedIndex + 1);
							infos[i].Height /= (mipbox.SelectedIndex + 1);
						}


						try
						{
							fs = new FileStream(imagepath + "[" + chunkval + "_bitmap resource handle_.chunk" + chunkval + "]", FileMode.Open, FileAccess.Read);
						}
						catch
						{
							WriteLog("could not find chunk file, double check you didnt rename anything");
						}


						bitm = new byte[fs.Length];
						fs.Read(bitm, 0, (int)fs.Length);

						fs.Close();
					}
					else
					{
						try
						{
							fs = new FileStream(imagepath, FileMode.Open, FileAccess.Read);
						}
						catch
						{
							WriteLog("could not find raw data file, double check you didnt rename anything");
						}

						fs.Position += 0x11C;

						bitm = new byte[fs.Length - 0x11C];
						fs.Read(bitm, 0, (int)fs.Length - 0x11C);

						fs.Close();
					}

					string savepath = path;

					if (checkpath.IsChecked == true)
					{
						savepath = pathbox.Text;

						if (!savepath.EndsWith("\\"))
							savepath += "\\";
					}

					if (checkpath.IsChecked == true && !pathbox.Text.EndsWith("\\"))
						pathbox.Text += "\\";

					FileStream outfile = new FileStream(savepath + filename + "_" + i + ".dds", FileMode.Create, FileAccess.Write);

					outfile.Write(DDSHeader1, 0, DDSHeader1.Length);

					byte[] value = BitConverter.GetBytes(infos[i].Height);

					outfile.Write(value, 0, 4);

					value = BitConverter.GetBytes(infos[i].Width);

					outfile.Write(value, 0, 4);

					value = BitConverter.GetBytes(infos[i].Width * infos[i].Height * 4);

					outfile.Write(value, 0, 4);

					outfile.Write(DDSHeader2, 0, DDSHeader2.Length);

					value = BitConverter.GetBytes(newformat);

					outfile.Write(value, 0, 4);

					outfile.Write(DDSHeader3, 0, DDSHeader3.Length);

					outfile.Write(bitm, 0, bitm.Length);

					outfile.Flush();
					outfile.Close();

					string statussaveloc = ".";

					if (checkpath.IsChecked == true)
						statussaveloc = " to output directory";


					WriteLog("file \"" + filename + "_" + i + ".dds\" saved" + statussaveloc);

					if (ctxwarning)
						ctxbitms.Add(filename);
					
				}

				foreach (string s in ctxbitms)
				{
					WriteLog("WARNING: Bitmap " + s + " was extracted with a guessed format, report this message with bitmap name to me on twitter!");
				}
			}

		}

		public int GetFormat(int tagformat)
		{
			switch (tagformat)
			{
				case 0x0:
					return 65;
				case 0x1:
				case 0x2:
					return 61;
				case 0x3:
					return 49;
				case 0xB:
					return 0x57;
				case 0xE:
					return 0x47;
				case 0xF:
					return 74;
				case 0x10:
					return 0x4D;
				case 0x18:
					return 0x2;
				case 0x19:
					return 10;
				case 0x1c:
					return 31;
				case 0x20:
					return 56;
				case 0x24:
				case 0x2B:
				case 0x2C:
					return 80;
				case 0x25:
					return 81;
				case 0x27:
					return 0x54;
				case 0x2D:
					return 83;
				case 0x2E:
					return 84;
				case 0x31:
					return 0x61;
				default:
					return -1;

				case 0x28:
					return 49; // This is a guess, tag defs claim this format is deprecated, yet it is still used. gg


			}
		}


		public class bitminfo
		{
			public int Width { get; set; }
			public int Height { get; set; }
			public int Format { get; set; }
		}

		public void WriteLog(string input)
		{
			statustext.AppendText(input + "\r\n");

			statustext.ScrollToEnd();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();
			if (result.ToString() == "OK")
				pathbox.Text = folderDialog.SelectedPath + "\\";
		}
	}

}
