const path = require('path');

module.exports = {
    entry: './src/index.js', //entry: './src/index.js'
  output: {
    filename: 'bundle.js',
    path: path.resolve(__dirname, 'public'),
    library: 'add_on',
      libraryTarget: 'umd',       // universal module definition
      globalObject: 'this' 
  },
  mode: 'production'
};
