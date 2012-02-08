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
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.Framework.UserManagement;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Avatar;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.AvatarService;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.OptionalModules.World.NPC.Tests
{
    [TestFixture]
    public class NPCModuleTests
    {
        private TestScene scene;
        private AvatarFactoryModule afm;
        private UserManagementModule umm;
        private AttachmentsModule am;

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public void Init()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("NPC");
            config.Configs["NPC"].Set("Enabled", "true");
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            afm = new AvatarFactoryModule();
            umm = new UserManagementModule();
            am = new AttachmentsModule();

            scene = SceneHelpers.SetupScene();
            SceneHelpers.SetupSceneModules(scene, config, afm, umm, am, new BasicInventoryAccessModule(), new NPCModule());
        }

        [Test]
        public void TestCreate()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            // 8 is the index of the first baked texture in AvatarAppearance
            UUID originalFace8TextureId = TestHelpers.ParseTail(0x10);
            Primitive.TextureEntry originalTe = new Primitive.TextureEntry(UUID.Zero);
            Primitive.TextureEntryFace originalTef = originalTe.CreateFace(8);
            originalTef.TextureID = originalFace8TextureId;

            // We also need to add the texture to the asset service, otherwise the AvatarFactoryModule will tell
            // ScenePresence.SendInitialData() to reset our entire appearance.
            scene.AssetService.Store(AssetHelpers.CreateNotecardAsset(originalFace8TextureId));

            afm.SetAppearance(sp, originalTe, null);

            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, scene, sp.Appearance);

            ScenePresence npc = scene.GetScenePresence(npcId);

            Assert.That(npc, Is.Not.Null);
            Assert.That(npc.Appearance.Texture.FaceTextures[8].TextureID, Is.EqualTo(originalFace8TextureId));
            Assert.That(umm.GetUserName(npc.UUID), Is.EqualTo(string.Format("{0} {1}", npc.Firstname, npc.Lastname)));
        }

        [Test]
        public void TestRemove()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            Vector3 startPos = new Vector3(128, 128, 30);
            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", startPos, UUID.Zero, true, scene, sp.Appearance);

            npcModule.DeleteNPC(npcId, scene);

            ScenePresence deletedNpc = scene.GetScenePresence(npcId);

            Assert.That(deletedNpc, Is.Null);
        }

        [Test]
        public void TestAttachments()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, userId);

            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserInventoryHelpers.CreateInventoryItem(scene, attName, attItemId, attAssetId, sp.UUID, InventoryType.Object);

            am.RezSingleAttachmentFromInventory(sp, attItemId, (uint)AttachmentPoint.Chest);

            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, scene, sp.Appearance);

            ScenePresence npc = scene.GetScenePresence(npcId);

            // Check scene presence status
            Assert.That(npc.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = npc.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];

            // Just for now, we won't test the name since this is (wrongly) the asset part name rather than the item
            // name.  TODO: Do need to fix ultimately since the item may be renamed before being passed on to an NPC.
//            Assert.That(attSo.Name, Is.EqualTo(attName));

            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);
            Assert.That(attSo.OwnerID, Is.EqualTo(npc.UUID));
        }

        [Test]
        public void TestMove()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            Vector3 startPos = new Vector3(128, 128, 30);
            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", startPos, UUID.Zero, true, scene, sp.Appearance);

            ScenePresence npc = scene.GetScenePresence(npcId);
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            // For now, we'll make the scene presence fly to simplify this test, but this needs to change.
            npc.Flying = true;

            scene.Update();
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            Vector3 targetPos = startPos + new Vector3(0, 10, 0);
            npcModule.MoveToTarget(npc.UUID, scene, targetPos, false, false);

            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));
            //Assert.That(npc.Rotation, Is.EqualTo(new Quaternion(0, 0, 0.7071068f, 0.7071068f)));
            Assert.That(
                npc.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0.7071068f, 0.7071068f), 0.000001));

            scene.Update();

            // We should really check the exact figure.
            Assert.That(npc.AbsolutePosition.X, Is.EqualTo(startPos.X));
            Assert.That(npc.AbsolutePosition.Y, Is.GreaterThan(startPos.Y));
            Assert.That(npc.AbsolutePosition.Z, Is.EqualTo(startPos.Z));
            Assert.That(npc.AbsolutePosition.Z, Is.LessThan(targetPos.X));

            for (int i = 0; i < 10; i++)
                scene.Update();

            double distanceToTarget = Util.GetDistanceTo(npc.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "NPC not within 1 unit of target position on first move");
            Assert.That(npc.AbsolutePosition, Is.EqualTo(targetPos));
            Assert.That(npc.AgentControlFlags, Is.EqualTo((uint)AgentManager.ControlFlags.NONE));

            // Try a second movement
            startPos = npc.AbsolutePosition;
            targetPos = startPos + new Vector3(10, 0, 0);
            npcModule.MoveToTarget(npc.UUID, scene, targetPos, false, false);

            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));
//            Assert.That(npc.Rotation, Is.EqualTo(new Quaternion(0, 0, 0, 1)));
            Assert.That(
                npc.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0, 1), 0.000001));

            scene.Update();

            // We should really check the exact figure.
            Assert.That(npc.AbsolutePosition.X, Is.GreaterThan(startPos.X));
            Assert.That(npc.AbsolutePosition.X, Is.LessThan(targetPos.X));
            Assert.That(npc.AbsolutePosition.Y, Is.EqualTo(startPos.Y));
            Assert.That(npc.AbsolutePosition.Z, Is.EqualTo(startPos.Z));

            for (int i = 0; i < 10; i++)
                scene.Update();

            distanceToTarget = Util.GetDistanceTo(npc.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "NPC not within 1 unit of target position on second move");
            Assert.That(npc.AbsolutePosition, Is.EqualTo(targetPos));
        }

        [Test]
        public void TestSitAndStandWithSitTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));

            Vector3 startPos = new Vector3(128, 128, 30);
            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", startPos, UUID.Zero, true, scene, sp.Appearance);

            ScenePresence npc = scene.GetScenePresence(npcId);
            SceneObjectPart part = SceneHelpers.AddSceneObject(scene);

            part.SitTargetPosition = new Vector3(0, 0, 1);
            npcModule.Sit(npc.UUID, part.UUID, scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(npcId));
            Assert.That(npc.ParentID, Is.EqualTo(part.LocalId));
            Assert.That(
                npc.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + part.SitTargetPosition + ScenePresence.SIT_TARGET_ADJUSTMENT));

            npcModule.Stand(npc.UUID, scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(0));
        }

        [Test]
        public void TestSitAndStandWithNoSitTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));

            // FIXME: To get this to work for now, we are going to place the npc right next to the target so that
            // the autopilot doesn't trigger
            Vector3 startPos = new Vector3(1, 1, 1);

            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", startPos, UUID.Zero, true, scene, sp.Appearance);

            ScenePresence npc = scene.GetScenePresence(npcId);
            SceneObjectPart part = SceneHelpers.AddSceneObject(scene);

            npcModule.Sit(npc.UUID, part.UUID, scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(part.LocalId));

            // FIXME: This is different for live avatars - z position is adjusted.  This is half the height of the
            // default avatar.
            // Curiously, Vector3.ToString() will not display the last two places of the float.  For example,
            // printing out npc.AbsolutePosition will give <0, 0, 0.8454993> not <0, 0, 0.845499337>
            Assert.That(
                npc.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + new Vector3(0, 0, 0.845499337f)));

            npcModule.Stand(npc.UUID, scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(0));
        }
    }
}