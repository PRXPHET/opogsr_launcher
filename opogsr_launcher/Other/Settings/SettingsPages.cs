global using SettingsControls = System.Collections.Generic.List<opogsr_launcher.Other.Settings.ISettingsControl>;

using System.Collections.Generic;

namespace opogsr_launcher.Other.Settings
{
    internal static class SettingsPages
    {
        private static readonly SettingsControls LauncherSettings = new()
        {
            { new SettingsCommands.SettingsControlLanguage("launcher_language", SettingsCommands.Langs, SettingsCommands.Langs.Contains(RuntimeResource.RuntimeResource.Instance.Language) ? RuntimeResource.RuntimeResource.Instance.Language : SettingsCommands.Langs[0]) },
            { new SettingsBool("move_to_system_tray_on_close", true, SettingType.Launcher) },
            { new SettingsSeparator("sep_launcher_warn") },
            { new SettingsRange<int>("max_download_threads", 1, 16, 1, 8, SettingType.Launcher) },
            { new SettingsRange<int>("max_amount_of_tries", 1, 20, 1, 5, SettingType.Launcher) },
            { new SettingsBool("allow_unsafe_connection", false, SettingType.Launcher) }
        };

        private static readonly SettingsControls VideoSettings = new()
        {
            { new SettingsRange<int>("r_fps_lock", 30, 500, 1, 60) },
            { new SettingsRange<int>("r_fps_lock_in_menu", 30, 500, 1, 60) },
            { new SettingsRange<double>("g_font_scale_x", 0.2, 5.0, 0.01, 1.0) },
            { new SettingsRange<double>("g_font_scale_y", 0.2, 5.0, 0.01, 1.0) },

            { new SettingsSeparator("sep_video_adv") },

            { new SettingsRange<int>("fov", 55, 110, 1, 75) },

            { new SettingsRange<int>("r__detail_radius", 70, 300, 1, 150) },
            { new SettingsRange<double>("r__detail_density", 0.06, 1.0, -0.01, 0.6) },
            { new SettingsRange<double>("r__detail_scale", 0.2, 2.0, 0.01, 1.1) },

            { new SettingsToken("r_aa_mode", SettingsCommands.AAToken, "st_opt_taa") },
            { new SettingsToken("r_ao_mode", SettingsCommands.AOToken, "st_ssdo") },
            { new SettingsToken("r_ao_quality", SettingsCommands.AOQualityToken, "st_opt_low") },

            { new SettingsTokenRaw("r__smap_size", SettingsCommands.SmapToken, "2048x2048") },
            { new SettingsToken("r2_sun_quality", SettingsCommands.SunQualityToken, "st_opt_low") },
            { new SettingsToken("r_sunshafts_mode", SettingsCommands.SunshaftsModeToken, "ss_ogse") },

            { new SettingsBool("r__actor_shadow", true) },
            { new SettingsBool("r2_sun_details", false) },

            { new SettingsBool("exp_optimize_shadow_geom", false) },

             { new SettingsBool("r3_dynamic_wet_surfaces", true) },
             { new SettingsBool("r_sslr_enable", true) },

             { new SettingsBool("r4_enable_tessellation", false) },

             { new SettingsBool("r2_steep_parallax", false) },

             { new SettingsBool("g_dof_reload", false) },
             { new SettingsBool("g_dof_zoom", true) },
             { new SettingsBool("motion_blur", true, SettingType.Script) },
             { new SettingsRange<double>("r2_mblur_intensity", 0.01, 2.0, 0.01, 0.5) },

             { new SettingsBool("r2_visor_refl", true) },
             { new SettingsBool("r2_rain_drops", true) },

             { new SettingsBool("r2_volumetric_lights", false) },
             { new SettingsBool("ai_use_torch_dynamic_lights", false) },

             { new SettingsBool("r_lens_flare", true) },

             { new SettingsBool("g_prefetch", true) },
        };

        private static readonly SettingsControls AudioSettings = new()
        {
            { new SettingsRange<double>("snd_volume", 0.0, 1.0, 0.01, 1.0) },
            { new SettingsRange<double>("snd_volume_effect", 0.0, 1.0, 0.01, 1.0) },
            { new SettingsRange<double>("snd_volume_music", 0.0, 1.0, 0.01, 1.0) },
            { new SettingsRange<double>("snd_volume_weapon", 0.0, 1.0, 0.01, 1.0) },
            { new SettingsRange<double>("snd_volume_ui", 0.0, 1.0, 0.01, 1.0) },
            { new SettingsBool("snd_efx", true) },
            { new SettingsBool("g_music_tracks", true) },
            { new SettingsBool("g_streamer_mode", false) },
        };

        private static readonly SettingsControls GameplaySettings = new()
        {
            { new SettingsToken("g_game_difficulty", SettingsCommands.GameDifficultyToken, "gd_novice") },
            { new SettingsBool("hud_crosshair", true) },
            { new SettingsBool("cl_dynamiccrosshair", true) },
            { new SettingsBool("hud_info", true) },
            { new SettingsBool("hud_crosshair_dist", false) },

            { new SettingsBool("fakescope_enable", true) },
            { new SettingsBool("g_3d_scopes", true) },
            //{ new SettingsBool("r_3dss_upscale_method", false) },
            { new SettingsBool("g_3d_pda", true) },

            { new SettingsBool("wpn_aim_toggle", false) },
            { new SettingsBool("g_mouse_wheel_switch_slot", false) },

            { new SettingsBool("rs_always_active", false) },

            { new SettingsSeparator("sep_op2") },

            { new SettingsRange<int>("inventory_window_delay", 100, 2000, 1, 700, SettingType.Script) },

            { new SettingsBool("critical_hit", true, SettingType.Script) },
            { new SettingsBool("animations", true, SettingType.Script) },
            { new SettingsBool("arachnophobia_mode", false, SettingType.Script) },
            { new SettingsBool("autosave_enable", true, SettingType.Script) },
            { new SettingsBool("dismemberment", true, SettingType.Script) },
            { new SettingsBool("opt_breath", true, SettingType.Script) },
            { new SettingsBool("fast_respawn", true, SettingType.Script) },
            { new SettingsBool("autograb_body", false, SettingType.Script) },
            { new SettingsBool("autograb_body_1", false, SettingType.Script) },
            { new SettingsBool("autograb_body_2", false, SettingType.Script) },
            { new SettingsBool("autograb_body_3", false, SettingType.Script) },
            { new SettingsBool("autograb_body_4", false, SettingType.Script) },
            { new SettingsBool("autograb_body_5", false, SettingType.Script) },
            { new SettingsBool("dynamic_bioradar", false, SettingType.Script) },
            { new SettingsBool("random_sms", true, SettingType.Script) },
            { new SettingsBool("new_sms", false, SettingType.Script) },

            { new SettingsSeparator("sep_opt_edit") },
            { new SettingsSeparator("sep_opt_edit_warn", "red") },

            { new SettingsBool("spot_treasures", false, SettingType.Script) },
            { new SettingsBool("varka_1min", false, SettingType.Script) },
            { new SettingsBool("disable_harvest_anm", false, SettingType.Script) },
            { new SettingsBool("disable_pickup_anm", false, SettingType.Script) },
        };

        public static readonly Dictionary<string, SettingsControls> Pages = new()
        {
            { "tab_launcher", LauncherSettings },
            { "tab_video", VideoSettings },
            { "tab_audio", AudioSettings },
            { "tab_gameplay", GameplaySettings },
        };
    }
}
