/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace pCampBot
{
    /// <summary>
    /// Thread/Bot manager for the application
    /// </summary>
    public class BotManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected CommandConsole m_console;
        protected List<PhysicsBot> m_lBot;
        protected Thread[] m_td;
        protected bool m_verbose = true;
        protected Random somthing = new Random(Environment.TickCount);
        protected int numbots = 0;
        public IConfig Config { get; private set; }

        /// <summary>
        /// Track the assets we have and have not received so we don't endlessly repeat requests.
        /// </summary>
        public Dictionary<UUID, bool> AssetsReceived { get; private set; }

        /// <summary>
        /// Constructor Creates MainConsole.Instance to take commands and provide the place to write data
        /// </summary>
        public BotManager()
        {
            AssetsReceived = new Dictionary<UUID, bool>();

            m_console = CreateConsole();
            MainConsole.Instance = m_console;

            // Make log4net see the console
            //
            ILoggerRepository repository = LogManager.GetRepository();
            IAppender[] appenders = repository.GetAppenders();
            OpenSimAppender consoleAppender = null;

            foreach (IAppender appender in appenders)
            {
                if (appender.Name == "Console")
                {
                    consoleAppender = (OpenSimAppender)appender;
                    consoleAppender.Console = m_console;
                    break;
                }
            }

            m_console.Commands.AddCommand("bot", false, "shutdown",
                    "shutdown",
                    "Shutdown bots and exit", HandleShutdown);

            m_console.Commands.AddCommand("bot", false, "quit",
                    "quit",
                    "Shutdown bots and exit",
                    HandleShutdown);

//            m_console.Commands.AddCommand("bot", false, "add bots",
//                    "add bots <number>",
//                    "Add more bots", HandleAddBots);

            m_lBot = new List<PhysicsBot>();
        }

        /// <summary>
        /// Startup number of bots specified in the starting arguments
        /// </summary>
        /// <param name="botcount">How many bots to start up</param>
        /// <param name="cs">The configuration for the bots to use</param>
        public void dobotStartup(int botcount, IConfig cs)
        {
            Config = cs;
            m_td = new Thread[botcount];

            string firstName = cs.GetString("firstname");
            string lastNameStem = cs.GetString("lastname");
            string password = cs.GetString("password");
            string loginUri = cs.GetString("loginuri");

            for (int i = 0; i < botcount; i++)
            {
                string lastName = string.Format("{0}_{1}", lastNameStem, i);
                startupBot(i, this, firstName, lastName, password, loginUri);
            }
        }

//        /// <summary>
//        /// Add additional bots (and threads) to our bot pool
//        /// </summary>
//        /// <param name="botcount">How Many of them to add</param>
//        public void addbots(int botcount)
//        {
//            int len = m_td.Length;
//            Thread[] m_td2 = new Thread[len + botcount];
//            for (int i = 0; i < len; i++)
//            {
//                m_td2[i] = m_td[i];
//            }
//            m_td = m_td2;
//            int newlen = len + botcount;
//            for (int i = len; i < newlen; i++)
//            {
//                startupBot(i, Config);
//            }
//        }

        /// <summary>
        /// This starts up the bot and stores the thread for the bot in the thread array
        /// </summary>
        /// <param name="pos">The position in the thread array to stick the bot's thread</param>
        /// <param name="cs">Configuration of the bot</param>
        /// <param name="firstName">First name</param>
        /// <param name="lastName">Last name</param>
        /// <param name="password">Password</param>
        /// <param name="loginUri">Login URI</param>
        public void startupBot(int pos, BotManager bm, string firstName, string lastName, string password, string loginUri)
        {
            PhysicsBot pb = new PhysicsBot(bm, firstName, lastName, password, loginUri);

            pb.OnConnected += handlebotEvent;
            pb.OnDisconnected += handlebotEvent;

            m_td[pos] = new Thread(pb.startup);
            m_td[pos].Name = pb.Name;
            m_td[pos].IsBackground = true;
            m_td[pos].Start();
            m_lBot.Add(pb);
        }

        /// <summary>
        /// High level connnected/disconnected events so we can keep track of our threads by proxy
        /// </summary>
        /// <param name="callbot"></param>
        /// <param name="eventt"></param>
        private void handlebotEvent(PhysicsBot callbot, EventType eventt)
        {
            switch (eventt)
            {
                case EventType.CONNECTED:
                    m_log.Info("[" + callbot.FirstName + " " + callbot.LastName + "]: Connected");
                    numbots++;
//                m_log.InfoFormat("NUMBOTS {0}", numbots);
                    break;
                case EventType.DISCONNECTED:
                    m_log.Info("[" + callbot.FirstName + " " + callbot.LastName + "]: Disconnected");
                    m_td[m_lBot.IndexOf(callbot)].Abort();
                    numbots--;
//                m_log.InfoFormat("NUMBOTS {0}", numbots);
                    if (numbots <= 0)
                        Environment.Exit(0);
                    break;
            }
        }

        /// <summary>
        /// Shutting down all bots
        /// </summary>
        public void doBotShutdown()
        {
            foreach (PhysicsBot pb in m_lBot)
            {
                pb.shutdown();
            }
        }

        /// <summary>
        /// Standard CreateConsole routine
        /// </summary>
        /// <returns></returns>
        protected CommandConsole CreateConsole()
        {
            return new LocalConsole("Region");
        }

        private void HandleShutdown(string module, string[] cmd)
        {
            m_log.Warn("[BOTMANAGER]: Shutting down bots");
            doBotShutdown();
        }

        /*
        private void HandleQuit(string module, string[] cmd)
        {
            m_console.Warn("DANGER", "This should only be used to quit the program if you've already used the shutdown command and the program hasn't quit");
            Environment.Exit(0);
        }
        */
//
//        private void HandleAddBots(string module, string[] cmd)
//        {
//            int newbots = 0;
//            
//            if (cmd.Length > 2)
//            {
//                Int32.TryParse(cmd[2], out newbots);
//            }
//            if (newbots > 0)
//                addbots(newbots);
//        }
    }
}
