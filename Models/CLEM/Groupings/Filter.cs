﻿using Models.CLEM.Interfaces;
using Models.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Display = Models.Core.DisplayAttribute;

namespace Models.CLEM.Groupings
{
    ///<summary>
    /// abstract base filter not used on its own
    ///</summary> 
    [Serializable]
    public abstract class Filter : CLEMModel
    {
        /// <inheritdoc/>
        [JsonIgnore]
        public new IFilterGroup Parent
        {
            get => base.Parent as IFilterGroup;
            set => base.Parent = value;
        }

        /// <summary>
        /// Filter operator
        /// </summary>
        [Description("Operator")]
        [Required]
        [Display(Type = DisplayType.DropDown, Values = nameof(GetOperators))]
        [System.ComponentModel.DefaultValueAttribute(ExpressionType.Equal)]
        public ExpressionType Operator { get; set; }
        
        /// <summary>
        /// Method to return avaialble operators
        /// </summary>
        /// <returns></returns>
        protected object[] GetOperators() => new object[]
        {
            ExpressionType.Equal,
            ExpressionType.NotEqual,
            ExpressionType.LessThan,
            ExpressionType.LessThanOrEqual,
            ExpressionType.GreaterThan,
            ExpressionType.GreaterThanOrEqual,
            ExpressionType.IsTrue,
            ExpressionType.IsFalse
        };

        /// <summary>
        /// Convert the operator to a symbol
        /// </summary>
        /// <returns>Operator as symbol</returns>
        protected string OperatorToSymbol()
        {
            switch (Operator)
            {
                case ExpressionType.Equal:
                    return "=";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.NotEqual:
                    return "!=";
                case ExpressionType.IsTrue:
                    return "is";
                case ExpressionType.IsFalse:
                    return "not";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Is operator a true false test
        /// </summary>
        /// <returns>Operator as symbol</returns>
        protected bool IsOperatorTrueFalseTest()
        {
            switch (Operator)
            {
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                    return (Value?.ToString().ToLower() == "true" | Value?.ToString().ToLower() == "false");
                case ExpressionType.IsTrue:
                case ExpressionType.IsFalse:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Value to check for filter
        /// </summary>
        [Description("Value to compare")]
        public object Value { get; set; }

        /// <summary>
        /// Takes the conditions set by the user and converts them to a logical test as a lambda expression
        /// </summary>
        public abstract Func<T, bool> Compile<T>() where T:IFilterable;
    }
}
