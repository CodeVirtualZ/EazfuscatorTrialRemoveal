using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.ComponentModel;
using System.Reflection;




namespace ETR
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
			worker.DoWork += worker_DoWork;
			worker.RunWorkerCompleted += worker_RunWorkerCompleted;
			worker.WorkerReportsProgress = true;
			worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
		}

        static String Filepath = null;
        static Int32 TypeIndex = -1;

		private readonly BackgroundWorker worker = new BackgroundWorker();

		private void worker_DoWork(object sender, DoWorkEventArgs e)
		{
			var worker = sender as BackgroundWorker;
			worker.ReportProgress(0, String.Format("Processing Start!"));
			Fixing(ModuleDefMD.Load(Filepath));
			worker.ReportProgress(100, "Done Processing.");
		}

		void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			DeterminateCircularProgress.Value = e.ProgressPercentage;
		}

		private void worker_RunWorkerCompleted(object sender,RunWorkerCompletedEventArgs e)
		{
			DeterminateCircularProgress.Value = 100;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".exe";
            dlg.Filter = ".NET Module (*.exe)|*.exe";
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                string filename = dlg.FileName;
                txt_assembly.Text = filename;
                Filepath = filename;
		worker.RunWorkerAsync();
				
            }
        }

		public void Fixing(ModuleDefMD module)
		{
			IList<TypeDef> evalTypes = FindTypes(module);
			worker.ReportProgress(25, "Finding EvaluationTypes");
			Thread.Sleep(1000);
			if (evalTypes.Count == 0)
			{
				this.Dispatcher.Invoke(() =>
				{
					label_done.Text = "Failed";
					label_done.Visibility = Visibility.Visible;
				});

			}
			else if (evalTypes.Count > 1)
			{
				foreach (var evalType in evalTypes)

				if (TypeIndex < 0)
				{
					TypeIndex = 0;
				}
				if (TypeIndex >= evalTypes.Count)
				{
					TypeIndex = 0;
				}
				Patching(evalTypes[TypeIndex]);
				worker.ReportProgress(55, "Patching");
				Thread.Sleep(1000);
				Writing(module);
			}
			else
			{
				Patching(evalTypes[0]);
				worker.ReportProgress(55, "Patching");
				Thread.Sleep(1000);
				Writing(module);
			}
		}

		public void Patching(TypeDef evalType)
		{
			var badMethod = GetStaticMethods(evalType, "System.Boolean", "System.Boolean")[0];
			var instructions = badMethod.Body.Instructions;
			instructions.Clear();
			instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
			instructions.Add(OpCodes.Ret.ToInstruction());
			badMethod.Body.ExceptionHandlers.Clear();
		}

		public IList<TypeDef> FindTypes(ModuleDefMD module)
		{
			var evalTypes = new List<TypeDef>();

			var types = module.GetTypes();
			foreach (var typeDef in types)
			{
				if(typeDef.Methods.Count == 6
				&& CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2
				&& CountStaticMethods(typeDef, "System.Void") == 2
				&& CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1
				&& CountStaticMethods(typeDef, "System.Boolean") == 1)
					evalTypes.Add(typeDef);
				else if (typeDef.Methods.Count == 4
				&& CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
				&& CountStaticMethods(typeDef, "System.Void") == 2
				&& CountStaticMethods(typeDef, "System.Boolean") == 1)
					evalTypes.Add(typeDef);
				else if (typeDef.Methods.Count == 3
				&& CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
				&& CountStaticMethods(typeDef, "System.Void") == 1
				&& CountStaticMethods(typeDef, "System.Boolean") == 1)
					evalTypes.Add(typeDef);
			}

			return evalTypes;
		}

		public Int32 CountStaticMethods(TypeDef def, String retType, params String[] paramTypes)
		{
			return GetStaticMethods(def, retType, paramTypes).Count;
		}

		public  IList<MethodDef> GetStaticMethods(TypeDef def, String retType, params String[] paramTypes)
		{
			List<MethodDef> methods = new List<MethodDef>();

			if (!def.HasMethods)
				return methods;

			foreach (var method in def.Methods)
			{
				if (!method.IsStatic)
					continue;
				if (!method.ReturnType.FullName.Equals(retType))
					continue;
				if (paramTypes.Length != method.Parameters.Count)
					continue;

				Boolean paramsMatch = true;
				for (Int32 i = 0; i < paramTypes.Length && i < method.Parameters.Count; i++)
				{
					if (!method.Parameters[i].Type.FullName.Equals(paramTypes[i]))
					{
						paramsMatch = false;
						break;
					}
				}

				if (!paramsMatch)
					continue;

				methods.Add(method);
			}

			return methods;
		}

		public void Writing(ModuleDefMD module)
		{
			worker.ReportProgress(80, "Saving");
			Thread.Sleep(500);
			String outputPath = GetOutput();
			Console.WriteLine("Saving {0}", outputPath);

			var options = new ModuleWriterOptions(module);
			options.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
			options.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
			module.Write(outputPath, options);
			this.Dispatcher.Invoke(() =>
			{
				label_done.Visibility = Visibility.Visible;
			});
			
		}

		public String GetOutput()
		{

			String dir = System.IO.Path.GetDirectoryName(Filepath);
			String noExt = System.IO.Path.GetFileNameWithoutExtension(Filepath);
			String ext = System.IO.Path.GetExtension(Filepath);
			String newFilename = String.Format("{0}-Removed{1}", noExt, ext);
			return System.IO.Path.Combine(dir, newFilename);
		}
	}
}
