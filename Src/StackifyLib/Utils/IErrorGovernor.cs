using StackifyLib.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackifyLib.Utils
{
    /// <summary>
    /// Handles error throttling from the client side appender
    /// </summary>
    public interface IErrorGovernor
    {
        bool ErrorShouldBeSent(StackifyError error);
    }
}
