/*
 * CURE frontend build.
 *
 * This file is a webpack configuration for the CURE frontend. It defines how the frontend code is bundled, optimized, and served during development and production.
 */
const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const webpack = require('webpack');

const SRC = path.resolve(__dirname, 'src');
const DIST = path.resolve(__dirname, 'dist');

module.exports = (env, argv) => {
  const isProduction = argv.mode === 'production';

  
  const apiBaseUrl = process.env.CURE_API_BASE_URL || '/api/v1';

  return {
    mode: isProduction ? 'production' : 'development',
    entry: path.join(SRC, 'index.js'),
    output: {
      path: DIST,
      // Content hashing lets the shell be cached aggressively while still
      // guaranteeing users receive new code after a deploy.
      filename: isProduction ? 'assets/[name].[contenthash:8].js' : 'assets/[name].js',
      chunkFilename: isProduction
        ? 'assets/[name].[contenthash:8].chunk.js'
        : 'assets/[name].chunk.js',
      assetModuleFilename: 'assets/[name].[contenthash:8][ext]',
      publicPath: '/',
      clean: true,
    },
    // Real source maps in production so frontend errors remain diagnosable
    
    devtool: isProduction ? 'source-map' : 'eval-cheap-module-source-map',
    resolve: {
      extensions: ['.js', '.jsx'],
      alias: {
        '@': SRC,
      },
    },
    module: {
      rules: [
        {
          test: /\.jsx?$/,
          include: SRC,
          use: {
            loader: 'babel-loader',
            options: { cacheDirectory: true },
          },
        },
        {
          // CSS modules for component-scoped styles.
          test: /\.module\.css$/,
          use: [
            isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
            {
              loader: 'css-loader',
              options: {
                importLoaders: 1,
                modules: {
                  localIdentName: isProduction
                    ? 'cure-[hash:base64:6]'
                    : '[name]__[local]',
                },
              },
            },
          ],
        },
        {
          // Global stylesheets: design tokens, resets, print styles.
          test: /\.css$/,
          exclude: /\.module\.css$/,
          use: [isProduction ? MiniCssExtractPlugin.loader : 'style-loader', 'css-loader'],
        },
        {
          test: /\.(png|jpe?g|gif|svg|woff2?|eot|ttf)$/i,
          type: 'asset/resource',
        },
      ],
    },
    plugins: [
      new HtmlWebpackPlugin({
        template: path.resolve(__dirname, 'public', 'index.html'),
        favicon: false,
        minify: isProduction && {
          collapseWhitespace: true,
          removeComments: true,
          keepClosingSlash: true,
        },
      }),
      new webpack.DefinePlugin({
        'process.env.NODE_ENV': JSON.stringify(isProduction ? 'production' : 'development'),
        'process.env.CURE_API_BASE_URL': JSON.stringify(apiBaseUrl),
      }),
      ...(isProduction
        ? [
            new MiniCssExtractPlugin({
              filename: 'assets/[name].[contenthash:8].css',
              chunkFilename: 'assets/[name].[contenthash:8].chunk.css',
            }),
          ]
        : []),
    ],
    optimization: {
      splitChunks: {
        chunks: 'all',
        cacheGroups: {
          // MUI and Emotion are large and stable — isolating them keeps the
          // vendor chunk cacheable across application deploys.
          mui: {
            test: /[\\/]node_modules[\\/](@mui|@emotion)[\\/]/,
            name: 'vendor-mui',
            priority: 20,
            reuseExistingChunk: true,
          },
          vendor: {
            test: /[\\/]node_modules[\\/]/,
            name: 'vendor',
            priority: 10,
            reuseExistingChunk: true,
          },
        },
      },
      runtimeChunk: isProduction ? 'single' : false,
    },
    performance: {
      // Budgets are advisory during the build; real numbers come from the
      // route-level metrics 
      hints: isProduction ? 'warning' : false,
      maxEntrypointSize: 900 * 1024,
      maxAssetSize: 900 * 1024,
    },
    stats: 'errors-warnings',
    devServer: {
      port: Number(process.env.PORT || 3000),
    
      historyApiFallback: true,
      hot: true,
      client: { overlay: { errors: true, warnings: false } },
      proxy: [
        {
          context: ['/api'],
          target: process.env.CURE_API_PROXY || 'http://localhost:8080',
          changeOrigin: true,
        },
      ],
    },
  };
};
