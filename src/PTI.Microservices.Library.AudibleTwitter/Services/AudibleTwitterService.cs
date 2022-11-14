using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Services.Specialized
{
    /// <summary>
    /// Service in charge of returning tweets in audio
    /// </summary>
    public class AudibleTwitterService
    {
        private TwitterService TwitterService { get; }
        private AzureSpeechService AzureSpeechService { get; }
        private AzureComputerVisionService AzureComputerVisionService { get; }

        /// <summary>
        /// Creates a new instance of <see cref="AudibleTwitterService"/>
        /// </summary>
        /// <param name="twitterService"></param>
        /// <param name="azureSpeechService"></param>
        /// <param name="azureComputerVisionService"></param>
        public AudibleTwitterService(TwitterService twitterService,
            AzureSpeechService azureSpeechService, AzureComputerVisionService azureComputerVisionService)
        {
            this.TwitterService = twitterService;
            this.AzureSpeechService = azureSpeechService;
            this.AzureComputerVisionService = azureComputerVisionService;
        }

        /// <summary>
        /// Gets a list of tweets and sends a built audio to the specified output stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="username"></param>
        /// <param name="maxTweets"></param>
        /// <param name="sinceTweetId"></param>
        /// <param name="includeRetweets"></param>
        /// <param name="excludeReplies"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task GetTweetsByUsernameToStreamAsync(Stream outputStream,
            string username,
            int maxTweets = 10, ulong sinceTweetId=1, bool includeRetweets=true,
            bool excludeReplies=true,

            CancellationToken cancellationToken=default)
        {
            var tweets = await this.TwitterService.
                GetTweetsByUsernameAsync(username, maxTweets, sinceTweetId, includeRetweets,
                excludeReplies, cancellationToken);
            try
            {
                await GenerateAudioFromTweets(outputStream, tweets);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Generates audio from a list of tweets
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="tweets"></param>
        /// <returns></returns>
        public async Task GenerateAudioFromTweets(Stream outputStream, List<LinqToTwitter.Status> tweets)
        {
            if (tweets.Count > 0)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"{tweets.Count} tweets found");
                int row = 1;
                foreach (var singleTweet in tweets)
                {
                    string tweetCompleteText = this.TwitterService.GetCompleteTweetText(singleTweet);
                    stringBuilder.AppendLine($"Result: {row}. Text: {tweetCompleteText}");
                    if (singleTweet.Entities != null && singleTweet.Entities.MediaEntities != null
                        && singleTweet.Entities.MediaEntities.Count > 0)
                    {
                        foreach (var singleMediaEntity in singleTweet.Entities.MediaEntities)
                        {
                            try
                            {
                                var mediaUrl = singleMediaEntity.MediaUrlHttps ?? singleMediaEntity.MediaUrl;
                                var imageAnalysisResult =
                                    await this.AzureComputerVisionService.AnalyzeImageAsync(new Uri(mediaUrl),
                                    visualFeatures: new System.Collections.Generic.List<AzureComputerVisionService.VisualFeature>() {
                                            AzureComputerVisionService.VisualFeature.Adult,
                                            AzureComputerVisionService.VisualFeature.Brands,
                                            AzureComputerVisionService.VisualFeature.Categories,
                                            AzureComputerVisionService.VisualFeature.Color,
                                            AzureComputerVisionService.VisualFeature.Description,
                                            AzureComputerVisionService.VisualFeature.Faces,
                                            AzureComputerVisionService.VisualFeature.ImageType,
                                            AzureComputerVisionService.VisualFeature.Objects,
                                            AzureComputerVisionService.VisualFeature.Tags
                                    },
                                    details: new System.Collections.Generic.List<AzureComputerVisionService.Details>() {
                                            AzureComputerVisionService.Details.Celebrities,
                                            AzureComputerVisionService.Details.Landmarks
                                    });
                                if (imageAnalysisResult.description.captions?.Length > 0)
                                {
                                    stringBuilder.AppendLine("Image Descriptions: ");
                                    foreach (var singleCaption in imageAnalysisResult.description.captions)
                                    {
                                        stringBuilder.AppendLine($"{singleCaption.text}");
                                    }
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                    }
                    row++;
                }
                await this.AzureSpeechService.TalkToStreamAsync(stringBuilder.ToString(), outputStream);
            }
        }
    }
}
