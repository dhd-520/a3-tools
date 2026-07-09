using System;
using A3Tools.Models;

namespace A3Tools.Common.DataAccess
{
    /// <summary>
    /// 数据访问工厂：根据账套的连接模式创建对应的 IDataAccess 实现
    /// </summary>
    public static class DataAccessFactory
    {
        /// <summary>
        /// 根据 Account 配置创建 IDataAccess
        /// </summary>
        public static IDataAccess Create(Account account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            switch (account.ConnectionMode)
            {
                case DataAccessMode.Direct:
                    return new DirectDataAccess(
                        account.ConnectionString,
                        account.Name);

                case DataAccessMode.Http:
                    if (string.IsNullOrEmpty(account.HttpEndpoint))
                        throw new InvalidOperationException("HttpEndpoint is required for Http mode");
                    if (string.IsNullOrEmpty(account.HttpSecretKey))
                        throw new InvalidOperationException("HttpSecretKey is required for Http mode");
                    if (string.IsNullOrEmpty(account.HttpServerPublicKey))
                        throw new InvalidOperationException("HttpServerPublicKey is required for Http mode");

                    return new HttpDataAccess(
                        account.HttpEndpoint,
                        account.ConnectionString,
                        account.HttpSecretKey,
                        account.HttpServerPublicKey,
                        account.Name);

                default:
                    throw new ArgumentException($"Unknown connection mode: {account.ConnectionMode}");
            }
        }
    }
}
