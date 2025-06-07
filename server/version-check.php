<?php

/**
 * Version upgrade path manager for SyncTrayzor
 * 
 * Clients request this with their current version, arch, and variant (portable, etc)
 * and this gives them a version to upgrade to (if any), along with the method of
 * upgrading to it (manual navigation to Github release page, automatic silent upgrade,
 * etc). 
 * 
 * $versions is a record of all of the current releases, which we might want to upgrade
 * people to. It has the structure:
 * [
 *    version => [
 *       variant => [
 *          'url' => [
 *             arch => 'url',
 *             ...
 *          ],
 *       ],
 *       ...
 *       'release_notes' => release_notes,
 *    ],
 *    ...
 * ]
 *
 * version: version string e.g. '1.2.3'
 * variant: e.g. 'portable', 'installed'. Matched against the variant provided by the
 *          client, or '*' can be used to specify a default.
 * arch:    e.g. 'x86', 'x64'. Matched against the arch provided by the client, or '*'
 *          can used to specify a default.
 * release_notes: Release notes to display to the user.
 * 
 * $upgrades is a map of old_version => new_version, and specifies the formatter to
 * use to communicate with old_version. It also allows various overrides to be
 * specified (e.g. release notes)
 * It has the structure:
 * [
 *    old_version => ['to' => new_version, 'formatter' => formatter_version, 'overrides' => [overrides]],
 *    ...
 * ]
 *
 * old_version: version being upgraded from
 * new_version: version to upgrade to
 * formatter_version: formatter version to use (in $response_formatters)
 * overrides: optional overrides, used by the formatter
 */

set_error_handler('error_handler');
date_default_timezone_set('UTC');
header('Content-Type: application/json');

// Allowed values for arch and variant
$allowed_arches = ['x64', 'arm64'];
$allowed_variants = ['installed', 'portable'];

function error_handler($severity, $message, $filename, $lineno)
{
   throw new ErrorException($message, 0, $severity, $filename, $lineno);
}

function get_with_wildcard($src, $value, $default = null)
{
   if (isset($src[$value]))
      return $src[$value];
   if (isset($src['*']))
      return $src['*'];
   return $default;
}

$versions = [
   '2.0.0' => [
      'base_url' => 'https://github.com/GermanCoding/SyncTrayzor/releases/download',
      'installed' => [
         'direct_download_url' => [
            'x64' => "{base_url}/v{version}/SyncTrayzorSetup-x64.exe",
            'arm64' => "{base_url}/v{version}/SyncTrayzorSetup-arm64.exe",
         ],
      ],
      'portable' => [
         'direct_download_url' => [
            'x64' => "{base_url}/v{version}/SyncTrayzorPortable-x64.zip",
            'arm' => "{base_url}/v{version}/SyncTrayzorPortable-arm64.zip",
         ],
      ],     
      'sha512sum_download_url' => "{base_url}/v{version}/sha512sum.txt.asc",
      'release_page_url' => 'https://github.com/GermanCoding/SyncTrayzor/releases/tag/v{version}',
      'release_notes' => "N/A",
   ]
];

$upgrades = [
   '1.1.29' => ['to' => 'latest', 'formatter' => '5']
];

$response_formatters = [
   // Base for 2.0.0 releases (same as the last 1.x releases)
   '5' => function($arch, $variant, $to_version, $to_version_info, $overrides)
   {
      $variant_info = isset($overrides[$variant]) ? get_with_wildcard($overrides, $variant) : get_with_wildcard($to_version_info, $variant);

      $data = [
         'version' => $to_version,
         'direct_download_url' => get_with_wildcard($variant_info['direct_download_url'], $arch),
         'sha512sum_download_url' => $to_version_info['sha512sum_download_url'],
         'release_page_url' => $to_version_info['release_page_url'],
         'release_notes' => isset($overrides['release_notes']) ? $overrides['release_notes'] : $to_version_info['release_notes'],
      ];

      return $data;
   },
];

$error = null;
$loggable_error = null;
$data = null;

try
{
   // Use filter_input for better input handling
   $version = isset($_GET['version']) ? trim(strip_tags($_GET['version'])) : null;
   $arch = isset($_GET['arch']) ? trim(strip_tags($_GET['arch'])) : null;
   $variant = isset($_GET['variant']) ? trim(strip_tags($_GET['variant'])) : null;

   // Validate inputs
   if (empty($version) || empty($arch) || empty($variant))
   {
      $error = ['code' => 1, 'message' => 'version, arch, or variant not specified'];
   }
   // Only allow known arches and variants
   else if (!in_array($arch, $allowed_arches, true) || !in_array($variant, $allowed_variants, true))
   {
      $error = ['code' => 3, 'message' => 'Invalid arch or variant'];
   }
   else if (isset($upgrades[$version]))
   {
      $to_version = $upgrades[$version]['to'];
      if ($to_version === 'latest')
         $to_version = array_keys($versions)[0];

      $formatter = $response_formatters[$upgrades[$version]['formatter']];
      $overrides = $upgrades[$version]['overrides'] ?? [];

      $base_url = $overrides['base_url'] ?? $versions[$to_version]['base_url'];

      array_walk_recursive($versions[$to_version], function(&$value, $key) use ($to_version, $base_url) {
         $value = str_replace('{version}', $to_version, $value);
         $value = str_replace('{base_url}', $base_url, $value);
      });
      $to_version_info = $versions[$to_version];

      $data = $formatter($arch, $variant, $to_version, $to_version_info, $overrides);
   }
}
catch (Exception $e)
{
   $error = ['code' => 2, 'message' => 'Unhandled error. Please try again later'];
   error_log($e->getMessage() . "\n" . $e->getTraceAsString());
}

$rsp = [];
if ($data != null)
   $rsp['data'] = $data;
if ($error != null)
   $rsp['error'] = $error;

$output = json_encode($rsp, JSON_UNESCAPED_SLASHES | JSON_FORCE_OBJECT | JSON_PRETTY_PRINT);

$date = date('c');

echo $output;
