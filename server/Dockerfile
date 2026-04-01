FROM php:8.5-apache@sha256:e55a9f8e4caa09c6a31ec752b307675d847bb8546d975f379128cb2a99842b96
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80