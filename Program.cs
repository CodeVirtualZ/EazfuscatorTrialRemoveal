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
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: ETR.exe <path-to-assembly>");
                return 1;
            }

            try
            {
                var module = ModuleDefMD.Load(args[0]);
                var evalTypes = FindTypes(module);

                if (evalTypes.Count == 0)
                {
                    Console.WriteLine("Version not supported.");
                    return 1;
                }

                var typeToProcess = evalTypes[0];
                Patching(typeToProcess);

                // First save as temporary -removed file
                string originalPath = args[0];
                string tempPath = originalPath.Replace(".exe", "-Removed.exe");

                var options = new ModuleWriterOptions(module);
                options.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
                options.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
                module.Write(tempPath, options);

                // Release module
                module.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Create backup folder and move original
                string backupDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(originalPath),
                    "Backup"
                );
                System.IO.Directory.CreateDirectory(backupDir);
                string backupPath = System.IO.Path.Combine(
                    backupDir,
                    System.IO.Path.GetFileName(originalPath)
                );

                // Move original to backup, delete original, rename temp to original
                System.IO.File.Copy(originalPath, backupPath, true);
                System.IO.File.Delete(originalPath);
                System.IO.File.Move(tempPath, originalPath);

                Console.WriteLine("Successfully patched and backed up original file.");
                return 0;
            }

            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 1;
            }
        }

        private static void Patching(TypeDef evalType)
        {
            var badMethod = GetStaticMethods(evalType, "System.Boolean", "System.Boolean").ToList();
            foreach (var method in badMethod)
            {
                var instructions = method.Body.Instructions;
                instructions.Clear();
                instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
                instructions.Add(OpCodes.Ret.ToInstruction());
                method.Body.ExceptionHandlers.Clear();
            }
        }

        private static IList<TypeDef> FindTypes(ModuleDefMD module)
        {
            var evalTypes = new List<TypeDef>();

            var types = module.GetTypes();
            foreach (var typeDef in types)
            {
                if (typeDef.Methods.Count == 7
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2
                && CountStaticMethods(typeDef, "System.Void") == 3
                && CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 6
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2
                && CountStaticMethods(typeDef, "System.Void") == 2
                && CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 4
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                && CountStaticMethods(typeDef, "System.Void") == 2
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 3
                && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                && CountStaticMethods(typeDef, "System.Void") == 1
                && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
            }

            return evalTypes;
        }

        private static Int32 CountStaticMethods(TypeDef def, String retType, params String[] paramTypes)
        {
            return GetStaticMethods(def, retType, paramTypes).Count;
        }

        private static IList<MethodDef> GetStaticMethods(TypeDef def, String retType, params String[] paramTypes)
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

        private static string GetOutput(string filepath)
        {
            // Create backup folder if it doesn't exist
            string backupDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(filepath),
                "Backup"
            );
            System.IO.Directory.CreateDirectory(backupDir);

            // Move original file to backup
            string backupFile = System.IO.Path.Combine(
                backupDir,
                System.IO.Path.GetFileName(filepath)
            );
            System.IO.File.Copy(filepath, backupFile, true);

            // Return original filepath to overwrite it
            return filepath;
        }

    }
}
