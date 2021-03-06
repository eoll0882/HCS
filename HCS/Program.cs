﻿using System;
using System.Linq;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using HCS.Service.Async.HouseManagement.v11_10_0_13;
using HCS.BaseTypes;
using HCS.Interfaces;
using HCS.Providers;
using HCS.Helpers;

namespace HCS
{
    class Program {
        static void Main(string[] args) {

            // выбераем сертификат
            var cert = GetCert();
            if (cert == null) return;

            Console.WriteLine("Укажите pin ЭЦП");
            string pin = string.Empty;//Console.ReadLine();

            // иницализируем менеджер конечных точек
            ServicePointConfig.InitConfig(cert.Item2, pin, cert.Item1);
            
            // создаем конфиг   
            var config = new ClientConfig {
                UseTunnel = false,
                IsPPAK = false,
                CertificateThumbprint = cert.Item2,
                OrgPPAGUID = "b14c8b87-6d0d-4854-a97c-74d34e1a8ca1",
                OrgEntityGUID = "c3ffd8b6-cda3-4eb5-9696-30fee607c8b3",
                Role = Globals.OrganizationRoles.UK
            };

            var request = new exportHouseDataRequest {
                RequestHeader = RequestHelper.Create<RequestHeader>(config.OrgPPAGUID,config.Role),
                exportHouseRequest = new exportHouseRequest {
                    Id = Globals.Constants.SignElementId,
                    FIASHouseGuid = "7263796e-1d5a-4535-8def-93315e8975db",
                }
            };

            try
            {

                IProvider provider = new HouseManagmentProvider(config);
                IGetStateResult result = new getStateResult();

                Console.WriteLine("Отправка запроса");
                var ack = provider.Send(request);

                Console.WriteLine($"Запрос принят в обработку, идентификатор ответа {ack.MessageGUID}");

                
                int attems = 1;

                while (true) {

                    Console.WriteLine($"Получение результата, попытка {attems}");

                    if (provider.TryGetResult(ack, out result))
                        break;

                    Thread.Sleep(5000);
                    attems++;
                }

                result.Items.OfType<ErrorMessageType>().ToList().ForEach(x => {
                    Console.WriteLine($"В ответе имеется ошибка {x.ErrorCode}{x.Description}");
                });

                result.Items.OfType<exportHouseResultType>().ToList().ForEach(x => {
                    Console.WriteLine($"Получен объект {x.HouseUniqueNumber}");
                });

                Console.WriteLine($"ОК");
            }
            catch (Exception ex)
            {
                Console.WriteLine("При отправке сообщения произошла ошибка:"+ex.GetBaseException().Message);
            }
            Console.ReadKey();
        }


        static Tuple<int,string> GetCert() {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var collection = store.Certificates
                    .OfType<X509Certificate2>()
                    .Where(x => x.HasPrivateKey && x.IsGostPrivateKey());
                var cert = X509Certificate2UI.SelectFromCollection(new X509Certificate2Collection(collection.ToArray()), "Выбор сертификата", "", X509SelectionFlag.SingleSelection)[0];
                Console.WriteLine($"Криптопровайдер:{cert.GetProviderType().Item2}");
                return new Tuple<int, string>(cert.GetProviderType().Item1,cert.Thumbprint);
            }
            catch
            {
                return null;
            }
            finally
            {
                store.Close();
            }
        }
    }

}
