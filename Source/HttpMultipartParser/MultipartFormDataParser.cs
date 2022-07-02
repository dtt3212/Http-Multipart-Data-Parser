// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MultipartFormDataParser.cs" company="Jake Woods">
//   Copyright (c) 2013 Jake Woods
//
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software
//   and associated documentation files (the "Software"), to deal in the Software without restriction,
//   including without limitation the rights to use, copy, modify, merge, publish, distribute,
//   sublicense, and/or sell copies of the Software, and to permit persons to whom the Software
//   is furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in all copies
//   or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//   INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
//   PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
//   ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
//   ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// <author>Jake Woods</author>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HttpMultipartParser
{
	/// <summary>
	///     Provides methods to parse a
	///     <see href="http://www.ietf.org/rfc/rfc2388.txt">
	///         <c>multipart/form-data</c>
	///     </see>
	///     stream into it's parameters and file data.
	/// </summary>
	/// <remarks>
	///     <para>
	///         A parameter is defined as any non-file data passed in the multipart stream. For example
	///         any form fields would be considered a parameter.
	///     </para>
	///     <para>
	///         The parser determines if a section is a file or not based on the presence or absence
	///         of the filename argument for the Content-Type header. If filename is set then the section
	///         is assumed to be a file, otherwise it is assumed to be parameter data.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code lang="C#">
	///       Stream multipartStream = GetTheMultipartStream();
	///       string boundary = GetTheBoundary();
	///       var parser = new MultipartFormDataParser(multipartStream, boundary, Encoding.UTF8);
	///
	///       // Grab the parameters (non-file data). Key is based on the name field
	///       var username = parser.Parameters["username"].Data;
	///       var password = parser.parameters["password"].Data;
	///
	///       // Grab the first files data
	///       var file = parser.Files.First();
	///       var filename = file.FileName;
	///       var filestream = file.Data;
	///   </code>
	///     <code lang="C#">
	///     // In the context of WCF you can get the boundary from the HTTP
	///     // request
	///     public ResponseClass MyMethod(Stream multipartData)
	///     {
	///         // First we need to get the boundary from the header, this is sent
	///         // with the HTTP request. We can do that in WCF using the WebOperationConext:
	///         var type = WebOperationContext.Current.IncomingRequest.Headers["Content-Type"];
	///
	///         // Now we want to strip the boundary out of the Content-Type, currently the string
	///         // looks like: "multipart/form-data; boundary=---------------------124123qase124"
	///         var boundary = type.Substring(type.IndexOf('=')+1);
	///
	///         // Now that we've got the boundary we can parse our multipart and use it as normal
	///         var parser = new MultipartFormDataParser(data, boundary, Encoding.UTF8);
	///
	///         ...
	///     }
	///   </code>
	/// </example>
	public class MultipartFormDataParser : IMultipartFormDataParser
	{
		#region Constants and fields

		/// <summary>
		///     The default buffer size.
		/// </summary>
		/// <remarks>
		///     4096 is the optimal buffer size as it matches the internal buffer of a StreamReader
		///     See: http://stackoverflow.com/a/129318/203133
		///     See: http://msdn.microsoft.com/en-us/library/9kstw824.aspx (under remarks).
		/// </remarks>
		private const int DefaultBufferSize = 4096;

		private readonly List<FilePart> _files;
		private readonly List<ParameterPart> _parameters;

		#endregion

		#region Constructors and Destructors

		/// <summary>
		///     Initializes a new instance of the <see cref="MultipartFormDataParser"/> class.
		/// </summary>
		private MultipartFormDataParser()
		{
			_files = new List<FilePart>();
			_parameters = new List<ParameterPart>();
		}

		#endregion

		#region Public Properties

		/// <summary>
		///     Gets the mapping of parameters parsed files. The name of a given field
		///     maps to the parsed file data.
		/// </summary>
		public IReadOnlyList<FilePart> Files => _files.AsReadOnly();

		/// <summary>
		///     Gets the parameters. Several ParameterParts may share the same name.
		/// </summary>
		public IReadOnlyList<ParameterPart> Parameters => _parameters.AsReadOnly();

		#endregion

		#region Static Methods

		/// <summary>
		///     Parse the stream into a new instance of the <see cref="MultipartFormDataParser" /> class
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParser"/> class.
		/// </returns>
		public static MultipartFormDataParser Parse(Stream stream, Encoding encoding, int binaryBufferSize = DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			return Parse(stream, null, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts);
		}

		/// <summary>
		///     Parse the stream into a new instance of the <see cref="MultipartFormDataParser" /> class
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="boundary">
		///     The multipart/form-data boundary. This should be the value
		///     returned by the request header.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParser"/> class.
		/// </returns>
		public static MultipartFormDataParser Parse(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			var parser = new MultipartFormDataParser();
			parser.ParseStream(stream, boundary, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts);
			return parser;
		}

		/// <summary>
		///     Asynchronously parse the stream into a new instance of the <see cref="MultipartFormDataParser" /> class
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParser"/> class.
		/// </returns>
		public static Task<MultipartFormDataParser> ParseAsync(Stream stream, Encoding encoding, int binaryBufferSize = DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			return ParseAsync(stream, null, encoding, DefaultBufferSize, null, ignoreInvalidParts);
		}

		/// <summary>
		///     Asynchronously parse the stream into a new instance of the <see cref="MultipartFormDataParser" /> class
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="boundary">
		///     The multipart/form-data boundary. This should be the value
		///     returned by the request header.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParser"/> class.
		/// </returns>
		public static async Task<MultipartFormDataParser> ParseAsync(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			var parser = new MultipartFormDataParser();
			await parser.ParseStreamAsync(stream, boundary, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts).ConfigureAwait(false);
			return parser;
		}

		#endregion

		#region Private Methods

		/// <summary>
		///     Parse the stream with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="boundary">
		///     The multipart/form-data boundary. This should be the value
		///     returned by the request header.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		private void ParseStream(Stream stream, string boundary, Encoding encoding, int binaryBufferSize, string[] binaryMimeTypes, bool ignoreInvalidParts)
		{
			var streamingParser = new StreamingMultipartFormDataParser(stream, boundary, encoding ?? Encoding.UTF8, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts);
			streamingParser.ParameterHandler += parameterPart => _parameters.Add(parameterPart);

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				if (partNumber == 0)
				{
					// create file with first partNo
					_files.Add(new FilePart(name, fileName, Utilities.MemoryStreamManager.GetStream($"{typeof(MultipartFormDataParser).FullName}.{nameof(ParseStream)}"), additionalProperties, type, disposition));
				}

				Files[Files.Count - 1].Data.Write(buffer, 0, bytes);
			};

			streamingParser.Run();

			// Reset all the written memory streams so they can be read.
			foreach (var file in Files)
			{
				file.Data.Position = 0;
			}
		}

		/// <summary>
		///     Parse the stream with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="boundary">
		///     The multipart/form-data boundary. This should be the value
		///     returned by the request header.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		private async Task ParseStreamAsync(Stream stream, string boundary, Encoding encoding, int binaryBufferSize, string[] binaryMimeTypes, bool ignoreInvalidParts)
		{
			var streamingParser = new StreamingMultipartFormDataParser(stream, boundary, encoding ?? Encoding.UTF8, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts);
			streamingParser.ParameterHandler += parameterPart => _parameters.Add(parameterPart);

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				if (partNumber == 0)
				{
					// create file with first partNo
					_files.Add(new FilePart(name, fileName, Utilities.MemoryStreamManager.GetStream($"{typeof(MultipartFormDataParser).FullName}.{nameof(ParseStreamAsync)}"), additionalProperties, type, disposition));
				}

				Files[Files.Count - 1].Data.Write(buffer, 0, bytes);
			};

			await streamingParser.RunAsync().ConfigureAwait(false);

			// Reset all the written memory streams so they can be read.
			foreach (var file in Files)
			{
				file.Data.Position = 0;
			}
		}

		#endregion
	}
}
