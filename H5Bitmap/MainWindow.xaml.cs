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
					WriteLog(Path.GetFileName(file) + " is not a .bitmap you dummy");
					continue;
				}

				//check how many raw images are in the same folder
				string path = @"\\?\" + Path.GetDirectoryName(file) + "\\";

				string filename = Path.GetFileNameWithoutExtension(file);

				string[] allfiles = Directory.GetFiles(path, filename + "*", SearchOption.TopDirectoryOnly);

				int resOffset = file.Contains("{ds}") ? 0x114 : 0x11C;

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

				if (bitmfiles.Count == 0)
				{
					if (file.Contains("{ds}"))
					{
						WriteLog(Path.GetFileName(file) + " has no raw data. H5 Server doesn't include many full bitmaps so this is likely expected.");
					}
					else
						WriteLog(Path.GetFileName(file) + " has no raw data. Your module might not have extracted fully.");

					continue;
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

						string chunkpath = imagepath + "[" + chunkval + "_bitmap resource handle_.chunk" + chunkval + "]";
						if (!File.Exists(chunkpath))
						{
							WriteLog("could not find chunk file, double check you didnt rename anything");
							continue;
						}
							
						try
						{
							bitm = File.ReadAllBytes(chunkpath);
						}
						catch
						{
							WriteLog("could not read chunk file, path could be too long");
							continue;
						}

					}
					else
					{
						if (!File.Exists(imagepath))
						{
							WriteLog("could not find raw data file, double check you didnt rename anything");
							continue;
						}

						try
						{
							fs = new FileStream(imagepath, FileMode.Open, FileAccess.Read);
						}
						catch
						{
							WriteLog("could not read raw data file, path could be too long");
						}

						fs.Position += resOffset;

						bitm = new byte[fs.Length - resOffset];
						fs.Read(bitm, 0, (int)fs.Length - resOffset);

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
				case 0x0: return 65;
				case 0x1:
				case 0x2: return 61;
				case 0x3: return 49;
				// 4 unused?
				// 5 unused?
				case 0x6: return 85;
				// 7 unused?
				case 0x8: return 86;
				case 0x9: return 115;
				case 0xA: return 88;
				case 0xB: return 87;
				// c unused?
				case 0xE: return 71;
				case 0xF: return 74;
				case 0x10: return 77;
				// 11 deprecated?
				// 12 unused?
				// 13 unused?
				// 14 deprecated?
				// 15 unused?
				case 0x16: return 51;
				// 17 deprecated?
				case 0x18: return 2;
				case 0x19: return 10;
				case 0x1A:
				case 0x1B: return 54;
				case 0x1C: return 31;
				case 0x1D: return 24;
				case 0x1E: return 11;
				case 0x1F: return 37;
				case 0x20: return 56;
				case 0x21: return 35;
				case 0x22: return 13;
				// 23 deprecated?
				case 0x24:
				case 0x2B:
				case 0x2C: return 80;
				case 0x25: return 81;
				// 26 deprecated?
				case 0x27: return 84;
				case 0x28: return 107; // This is a guess, tag defs claim this format is deprecated, yet it is still used. gg
				// 29 deprecated?
				// 2a deprecated?
				// 2b/2c is above 
				case 0x2D: return 83;
				case 0x2E: return 84;
				case 0x2F: return 95;
				case 0x30: return 96;
				case 0x31: return 97;
				case 0x32: return 45;
				case 0x33: return 26;

				default: return -1;

				


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
