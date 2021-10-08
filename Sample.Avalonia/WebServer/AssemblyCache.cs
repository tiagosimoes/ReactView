﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Sample.Avalonia.WebServer {
    internal class AssemblyCache {

        private object SyncRoot { get; } = new object();
        private Dictionary<string, Assembly> assemblies;
        private bool newAssembliesLoaded = true;

        internal Assembly ResolveResourceAssembly(string path, bool failOnMissingAssembly) {
            if (assemblies == null) {
                lock (SyncRoot) {
                    if (assemblies == null) {
                        assemblies = new Dictionary<string, Assembly>();
                        AppDomain.CurrentDomain.AssemblyLoad += delegate { newAssembliesLoaded = true; };
                    }
                }
            }

            var assemblyName = ResourceUrl.GetEmbeddedResourceAssemblyName(path);
            var assembly = GetAssemblyByName(assemblyName);

            if (assembly == null) {
                if (newAssembliesLoaded) {
                    lock (SyncRoot) {
                        if (newAssembliesLoaded) {
                            // add loaded assemblies to cache
                            newAssembliesLoaded = false;
                            foreach (var domainAssembly in AppDomain.CurrentDomain.GetAssemblies()) {
                                // replace if duplicated (can happen)
                                assemblies[domainAssembly.GetName().Name] = domainAssembly;
                            }
                        }
                    }
                }

                assembly = GetAssemblyByName(assemblyName);
                if (assembly == null) {
                    try {
                        // try load assembly from its name
                        var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
                        assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                    } catch (IOException) {
                        // ignore
                    }

                    if (assembly != null) {
                        lock (SyncRoot) {
                            assemblies[assembly.GetName().Name] = assembly;
                        }
                    }
                }
            }

            if (failOnMissingAssembly && assembly == null) {
                throw new InvalidOperationException("Could not find assembly for: " + path);
            }
            return assembly;
        }

        private Assembly GetAssemblyByName(string assemblyName) {
            lock (SyncRoot) {
                assemblies.TryGetValue(assemblyName, out var assembly);
                return assembly;
            }
        }
    }
}
