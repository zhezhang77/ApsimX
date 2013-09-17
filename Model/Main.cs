﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Model.Core;

namespace Model
{
    public class Program
    {
        /// <summary>
        /// Main program entry point.
        /// </summary>
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ApsimX .ApsimFileName");
                return 1;
            }

            try
            {
                Simulations Simulations = Utility.Xml.Deserialise(args[0]) as Simulations;
                if (Simulations == null)
                    throw new Exception("No simulations found in file: " + args[0]);
                Simulations.FileName = args[0];
                if (Simulations.Run())
                    return 0;
                else
                    return 1;

            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
                return 1;
            }
        }
    }
}