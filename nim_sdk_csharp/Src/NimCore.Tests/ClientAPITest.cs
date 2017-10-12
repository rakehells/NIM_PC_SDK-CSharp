// <copyright file="ClientAPITest.cs" company="NetEase Inc.">copyright (c) 2015-2016, NetEase Inc. All rights reserved</copyright>
using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NIM;
using NimUtility;

namespace NIM.Tests
{
    /// <summary>此类包含 ClientAPI 的参数化单元测试</summary>
    [PexClass(typeof(ClientAPI))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestClass]
    public partial class ClientAPITest
    {
        /// <summary>测试 Init(String, String, String, NimConfig) 的存根</summary>
        [PexMethod]
        public bool InitTest(
            string appKey,
            string appDataDir,
            string appInstallDir,
            NimConfig config
        )
        {
            bool result = ClientAPI.Init(appKey, appDataDir, appInstallDir, config);
            return result;
            // TODO: 将断言添加到 方法 ClientAPITest.InitTest(String, String, String, NimConfig)
        }
    }
}
