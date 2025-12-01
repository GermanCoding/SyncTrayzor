FROM php:8.4-apache@sha256:470a16f26251fa4a0ab07ac51f7db6137881a2510561e36454b1711dfde5e4f0
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80