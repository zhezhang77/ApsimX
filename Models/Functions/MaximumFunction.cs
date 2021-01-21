﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Models.Core;

namespace Models.Functions
{
    /// <summary>
    ///[Name] is calculated as the minimum of [ChildFunctionList]
    /// 
    /// Where:
    /// </summary>
    /// \pre All children have to contain a public function "Value"
    /// \retval Maximum value of all children of this node. Return -999999999 if no child.
    [Serializable]
    [Description("Returns the maximum value of all childern functions")]
    public class MaximumFunction : Model, IFunction
    {
        /// <summary>The child functions</summary>
        private IEnumerable<IFunction> ChildFunctions;

        /// <summary>Gets the value.</summary>
        /// <value>The value.</value>
        public double Value(int arrayIndex = -1)
        {
            if (ChildFunctions == null)
                ChildFunctions = FindAllChildren<IFunction>().ToList();

            double ReturnValue = -999999999;
            foreach (IFunction F in ChildFunctions)
            {
                ReturnValue = Math.Max(ReturnValue, F.Value(arrayIndex));
            }
            return ReturnValue;
        }
        /// <summary>String list of child functions</summary>
        public string ChildFunctionList
        {
            get
            {
                string listofKids = "";
                int count = 0;
                foreach (IModel F in this.FindAllChildren<IFunction>())
                {
                    count += 1;
                    listofKids += ("*"+F.Name+"*");
                    if (count == this.FindAllChildren<IFunction>().Count() - 1)
                        listofKids += " and ";
                    else if (count < this.FindAllChildren<IFunction>().Count() - 1)
                        listofKids += ", ";
                }
                return listofKids;
            }
        }
    }
}